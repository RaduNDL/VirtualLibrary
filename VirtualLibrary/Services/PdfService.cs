using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Net.Http;
using System.Text.Json;

namespace VirtualLibrary.Services
{
    public class PdfService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PdfService> _logger;

        public PdfService(IWebHostEnvironment env, IHttpClientFactory httpClientFactory, ILogger<PdfService> logger)
        {
            _env = env;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Search Open Library for a downloadable PDF link.
        /// Tries multiple strategies: ISBN lookup, then title+author search.
        /// </summary>
        public async Task<string> SearchOpenLibraryPdfAsync(string? isbn, string title, string author)
        {
            var client = _httpClientFactory.CreateClient("PdfClient");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("VirtualLibrary/1.0 (Student Project)");

            // ── Strategy 1: Search by ISBN first (most accurate) ──
            if (!string.IsNullOrEmpty(isbn))
            {
                var url = await TryOpenLibrarySearchAsync(client, $"isbn={isbn}", title);
                if (!string.IsNullOrEmpty(url)) return url;
            }

            // ── Strategy 2: Search by title + author ──
            var query = $"title={Uri.EscapeDataString(title)}";
            if (!string.IsNullOrWhiteSpace(author))
                query += $"&author={Uri.EscapeDataString(author)}";

            var url2 = await TryOpenLibrarySearchAsync(client, query, title);
            if (!string.IsNullOrEmpty(url2)) return url2;

            // ── Strategy 3: Search by title only ──
            var url3 = await TryOpenLibrarySearchAsync(client, $"title={Uri.EscapeDataString(title)}", title);
            return url3 ?? string.Empty;
        }

        private async Task<string?> TryOpenLibrarySearchAsync(HttpClient client, string query, string title)
        {
            try
            {
                var searchUrl = $"https://openlibrary.org/search.json?{query}&limit=5&fields=ia,title";
                _logger.LogInformation("Open Library search: {Url}", searchUrl);

                var response = await client.GetAsync(searchUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Open Library search failed: {Status}", response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                var numFound = root.GetProperty("numFound").GetInt32();
                _logger.LogInformation("Open Library found {Count} results for '{Title}'", numFound, title);

                if (numFound == 0) return null;

                var docs = root.GetProperty("docs");
                foreach (var bookDoc in docs.EnumerateArray())
                {
                    if (!bookDoc.TryGetProperty("ia", out var iaArray) || iaArray.GetArrayLength() == 0)
                        continue;

                    // Try each Internet Archive identifier
                    foreach (var iaItem in iaArray.EnumerateArray())
                    {
                        var identifier = iaItem.GetString();
                        if (string.IsNullOrEmpty(identifier)) continue;

                        // ── Try multiple PDF URL patterns that Archive.org uses ──
                        var urls = new[]
                        {
                            $"https://archive.org/download/{identifier}/{identifier}.pdf",
                            $"https://archive.org/download/{identifier}/{identifier}_text.pdf",
                        };

                        foreach (var pdfUrl in urls)
                        {
                            if (await VerifyUrlIsAccessibleAsync(client, pdfUrl))
                            {
                                _logger.LogInformation("✓ Verified PDF URL: {Url}", pdfUrl);
                                return pdfUrl;
                            }
                        }

                        // ── Try to find any .pdf file in the Archive.org metadata ──
                        var metaUrl = $"https://archive.org/metadata/{identifier}/files";
                        try
                        {
                            var metaResp = await client.GetAsync(metaUrl);
                            if (metaResp.IsSuccessStatusCode)
                            {
                                var metaJson = await metaResp.Content.ReadAsStringAsync();
                                using var metaDoc = JsonDocument.Parse(metaJson);

                                if (metaDoc.RootElement.TryGetProperty("result", out var files))
                                {
                                    foreach (var file in files.EnumerateArray())
                                    {
                                        if (file.TryGetProperty("name", out var nameProp))
                                        {
                                            var fileName = nameProp.GetString();
                                            if (fileName != null && fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                                            {
                                                var realPdfUrl = $"https://archive.org/download/{identifier}/{Uri.EscapeDataString(fileName)}";
                                                _logger.LogInformation("✓ Found PDF in metadata: {Url}", realPdfUrl);
                                                return realPdfUrl;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Metadata check failed for {Id}", identifier);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error searching Open Library");
            }

            return null;
        }

        /// <summary>
        /// Verify a URL is accessible and returns a PDF-like content type (HEAD request).
        /// </summary>
        private async Task<bool> VerifyUrlIsAccessibleAsync(HttpClient client, string url)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("HEAD {Url} → {Status}", url, response.StatusCode);
                    return false;
                }

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                var contentLength = response.Content.Headers.ContentLength ?? 0;

                // Must be PDF content type and at least 10KB (real PDFs are bigger)
                var isPdf = contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
                         || contentType.Contains("octet-stream", StringComparison.OrdinalIgnoreCase);
                var isLargeEnough = contentLength > 10_000;

                _logger.LogDebug("HEAD {Url} → {Type}, {Size} bytes, isPdf={IsPdf}", url, contentType, contentLength, isPdf && isLargeEnough);

                return isPdf && isLargeEnough;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Download a PDF from a URL and save it locally.
        /// </summary>
        public async Task<string> DownloadAndSavePdfAsync(int productId, string url, string source)
        {
            var client = _httpClientFactory.CreateClient("PdfClient");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("VirtualLibrary/1.0 (Student Project)");
            client.Timeout = TimeSpan.FromMinutes(5); // PDFs can be large

            try
            {
                _logger.LogInformation("Downloading PDF: {Url}", url);

                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("PDF download failed: {Status} from {Url}", response.StatusCode, url);
                    return string.Empty;
                }

                var data = await response.Content.ReadAsByteArrayAsync();
                _logger.LogInformation("Downloaded {Size} bytes from {Url}", data.Length, url);

                // Must be at least 1KB to be a real PDF
                if (data.Length < 1024)
                {
                    _logger.LogWarning("Downloaded file too small ({Size} bytes), probably not a real PDF", data.Length);
                    return string.Empty;
                }

                // Check if it starts with %PDF OR if content-type was pdf (some servers gzip it)
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                var startsWithPdf = data.Length >= 4 && data[0] == 0x25 && data[1] == 0x50 && data[2] == 0x44 && data[3] == 0x46;
                var isContentTypePdf = contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
                                    || contentType.Contains("octet-stream", StringComparison.OrdinalIgnoreCase);

                if (!startsWithPdf && !isContentTypePdf)
                {
                    // Check if it's HTML (common error page from Archive.org)
                    var firstBytes = System.Text.Encoding.UTF8.GetString(data, 0, Math.Min(200, data.Length));
                    if (firstBytes.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
                        firstBytes.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("Downloaded HTML instead of PDF from {Url}", url);
                        return string.Empty;
                    }

                    _logger.LogWarning("File doesn't look like a PDF (starts with: {Start}, content-type: {Type})",
                        BitConverter.ToString(data, 0, Math.Min(4, data.Length)), contentType);
                    return string.Empty;
                }

                // ── Save to disk ──
                var pdfDir = Path.Combine(_env.WebRootPath, "pdfs");
                if (!Directory.Exists(pdfDir)) Directory.CreateDirectory(pdfDir);

                var fileName = $"{productId}_auto_{Guid.NewGuid():N}.pdf";
                var filePath = Path.Combine(pdfDir, fileName);

                await File.WriteAllBytesAsync(filePath, data);
                _logger.LogInformation("✓ PDF saved: {Path} ({Size} bytes)", filePath, data.Length);

                return Path.Combine("pdfs", fileName).Replace('\\', '/');
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading PDF from {Url}", url);
                return string.Empty;
            }
        }

        public async Task<string> UploadPdfAsync(int productId, IFormFile file)
        {
            if (file == null || file.Length == 0) return string.Empty;

            var pdfDir = Path.Combine(_env.WebRootPath, "pdfs");
            if (!Directory.Exists(pdfDir)) Directory.CreateDirectory(pdfDir);

            var fileName = $"{productId}_{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(pdfDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("✓ PDF uploaded: {Path} ({Size} bytes)", filePath, file.Length);
            return Path.Combine("pdfs", fileName).Replace('\\', '/');
        }

        public async Task<string> ExtractTextAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(filePath)) return string.Empty;
                    using var pdfReader = new PdfReader(filePath);
                    using var pdfDoc = new iText.Kernel.Pdf.PdfDocument(pdfReader);
                    var text = new System.Text.StringBuilder();

                    for (int i = 1; i <= Math.Min(pdfDoc.GetNumberOfPages(), 10); i++)
                    {
                        var strategy = new SimpleTextExtractionStrategy();
                        var pageText = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(i), strategy);
                        text.Append(pageText);
                    }
                    return text.ToString();
                }
                catch { return string.Empty; }
            });
        }

        public async Task<bool> DeletePdfAsync(int productId)
        {
            await Task.CompletedTask;
            var pdfDir = Path.Combine(_env.WebRootPath, "pdfs");
            if (!Directory.Exists(pdfDir)) return false;

            var files = Directory.GetFiles(pdfDir, $"{productId}_*");
            if (files.Length == 0) return false;

            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                    _logger.LogInformation("Deleted PDF: {Path}", file);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete: {Path}", file);
                }
            }
            return true;
        }
    }
}