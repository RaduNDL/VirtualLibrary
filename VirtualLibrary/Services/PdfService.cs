using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace VirtualLibrary.Services
{
    public class PdfService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PdfService> _logger;

        public PdfService(
            IWebHostEnvironment env,
            IHttpClientFactory httpClientFactory,
            ILogger<PdfService> logger)
        {
            _env = env;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string> SearchOpenLibraryPdfAsync(string? isbn, string title, string author)
        {
            var client = _httpClientFactory.CreateClient("PdfClient");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("VirtualLibrary/1.0 (Student Project)");

            if (!string.IsNullOrWhiteSpace(isbn))
            {
                var byIsbn = await TryOpenLibrarySearchAsync(client, $"isbn={Uri.EscapeDataString(isbn.Trim())}", title);
                if (!string.IsNullOrWhiteSpace(byIsbn))
                    return byIsbn;
            }

            if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(author))
            {
                var byTitleAuthor = await TryOpenLibrarySearchAsync(
                    client,
                    $"title={Uri.EscapeDataString(title.Trim())}&author={Uri.EscapeDataString(author.Trim())}",
                    title);

                if (!string.IsNullOrWhiteSpace(byTitleAuthor))
                    return byTitleAuthor;
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                var byTitle = await TryOpenLibrarySearchAsync(
                    client,
                    $"title={Uri.EscapeDataString(title.Trim())}",
                    title);

                if (!string.IsNullOrWhiteSpace(byTitle))
                    return byTitle;
            }

            return string.Empty;
        }

        private async Task<string?> TryOpenLibrarySearchAsync(HttpClient client, string query, string title)
        {
            try
            {
                var searchUrl = $"https://openlibrary.org/search.json?{query}&limit=8&fields=ia,title";
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

                if (!root.TryGetProperty("docs", out var docs) || docs.ValueKind != JsonValueKind.Array)
                    return null;

                foreach (var bookDoc in docs.EnumerateArray())
                {
                    if (!bookDoc.TryGetProperty("ia", out var iaArray) ||
                        iaArray.ValueKind != JsonValueKind.Array ||
                        iaArray.GetArrayLength() == 0)
                    {
                        continue;
                    }

                    foreach (var iaItem in iaArray.EnumerateArray())
                    {
                        var identifier = iaItem.GetString();
                        if (string.IsNullOrWhiteSpace(identifier))
                            continue;

                        var directCandidates = new[]
                        {
                            $"https://archive.org/download/{identifier}/{identifier}.pdf",
                            $"https://archive.org/download/{identifier}/{identifier}_text.pdf"
                        };

                        foreach (var candidateUrl in directCandidates)
                        {
                            if (await VerifyUrlIsAccessibleAsync(client, candidateUrl))
                            {
                                _logger.LogInformation("Verified PDF URL for '{Title}': {Url}", title, candidateUrl);
                                return candidateUrl;
                            }
                        }

                        var metadataUrl = $"https://archive.org/metadata/{identifier}";
                        try
                        {
                            var metadataResponse = await client.GetAsync(metadataUrl);
                            if (!metadataResponse.IsSuccessStatusCode)
                                continue;

                            var metadataJson = await metadataResponse.Content.ReadAsStringAsync();
                            using var metadataDoc = JsonDocument.Parse(metadataJson);
                            var metadataRoot = metadataDoc.RootElement;

                            if (!metadataRoot.TryGetProperty("files", out var files) ||
                                files.ValueKind != JsonValueKind.Array)
                            {
                                continue;
                            }

                            foreach (var file in files.EnumerateArray())
                            {
                                if (!file.TryGetProperty("name", out var nameProp))
                                    continue;

                                var fileName = nameProp.GetString();
                                if (string.IsNullOrWhiteSpace(fileName) ||
                                    !fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                var encodedFileName = Uri.EscapeDataString(fileName);
                                var pdfUrl = $"https://archive.org/download/{identifier}/{encodedFileName}";

                                if (await VerifyUrlIsAccessibleAsync(client, pdfUrl))
                                {
                                    _logger.LogInformation("Found metadata PDF for '{Title}': {Url}", title, pdfUrl);
                                    return pdfUrl;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Metadata lookup failed for archive identifier {Identifier}", identifier);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error searching Open Library for title {Title}", title);
            }

            return null;
        }

        private async Task<bool> VerifyUrlIsAccessibleAsync(HttpClient client, string url)
        {
            try
            {
                using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
                using var headResponse = await client.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead);

                if (headResponse.IsSuccessStatusCode)
                {
                    var contentType = headResponse.Content.Headers.ContentType?.MediaType ?? string.Empty;
                    var contentLength = headResponse.Content.Headers.ContentLength ?? 0;

                    if (LooksLikePdf(contentType, null) && contentLength > 1024)
                        return true;
                }
            }
            catch
            {
            }

            try
            {
                using var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
                getRequest.Headers.Range = new RangeHeaderValue(0, 1023);

                using var getResponse = await client.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead);
                if (!getResponse.IsSuccessStatusCode)
                    return false;

                var contentType = getResponse.Content.Headers.ContentType?.MediaType ?? string.Empty;
                await using var stream = await getResponse.Content.ReadAsStreamAsync();

                var buffer = new byte[8];
                var read = await stream.ReadAsync(buffer, 0, buffer.Length);

                return LooksLikePdf(contentType, buffer.Take(read).ToArray());
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> DownloadAndSavePdfAsync(int productId, string url, string source)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
                return string.Empty;

            var client = _httpClientFactory.CreateClient("PdfClient");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("VirtualLibrary/1.0 (Student Project)");
            client.Timeout = TimeSpan.FromMinutes(5);

            try
            {
                _logger.LogInformation("Downloading PDF from {Url}", url);

                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("PDF download failed: {Status} from {Url}", response.StatusCode, url);
                    return string.Empty;
                }

                var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                var data = await response.Content.ReadAsByteArrayAsync();

                if (data.Length < 1024)
                {
                    _logger.LogWarning("Downloaded file is too small to be a valid PDF: {Size} bytes", data.Length);
                    return string.Empty;
                }

                if (!LooksLikePdf(contentType, data.Take(8).ToArray()))
                {
                    var firstChunk = Encoding.UTF8.GetString(data, 0, Math.Min(256, data.Length));
                    if (firstChunk.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
                        firstChunk.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("Downloaded HTML instead of PDF from {Url}", url);
                        return string.Empty;
                    }

                    _logger.LogWarning("Downloaded content does not look like a PDF from {Url}", url);
                    return string.Empty;
                }

                var booksDir = Path.Combine(_env.WebRootPath, "pdfs", "books");
                Directory.CreateDirectory(booksDir);

                var safeSource = SanitizeToken(source);
                var fileName = $"{productId}_{safeSource}_{Guid.NewGuid():N}.pdf";
                var absolutePath = Path.Combine(booksDir, fileName);

                await File.WriteAllBytesAsync(absolutePath, data);

                var relativePath = Path.Combine("pdfs", "books", fileName).Replace('\\', '/');
                _logger.LogInformation("Saved PDF for product {ProductId} to {Path}", productId, relativePath);

                return relativePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading PDF from {Url}", url);
                return string.Empty;
            }
        }

        public async Task<string> UploadPdfAsync(int productId, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return string.Empty;

            if (file.Length > 104_857_600)
                return string.Empty;

            var ext = Path.GetExtension(file.FileName);
            if (!".pdf".Equals(ext, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            byte[] data;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                data = ms.ToArray();
            }

            if (data.Length < 1024 || !LooksLikePdf(file.ContentType ?? string.Empty, data.Take(8).ToArray()))
                return string.Empty;

            var booksDir = Path.Combine(_env.WebRootPath, "pdfs", "books");
            Directory.CreateDirectory(booksDir);

            var fileName = $"{productId}_manual_{Guid.NewGuid():N}.pdf";
            var absolutePath = Path.Combine(booksDir, fileName);

            await File.WriteAllBytesAsync(absolutePath, data);

            var relativePath = Path.Combine("pdfs", "books", fileName).Replace('\\', '/');
            _logger.LogInformation("Uploaded PDF for product {ProductId}: {Path}", productId, relativePath);

            return relativePath;
        }

        public async Task<string> ExtractTextAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                        return string.Empty;

                    using var pdfReader = new PdfReader(filePath);
                    using var pdfDoc = new iText.Kernel.Pdf.PdfDocument(pdfReader);
                    var text = new StringBuilder();

                    for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
                    {
                        var strategy = new SimpleTextExtractionStrategy();
                        var pageText = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(i), strategy);
                        text.Append(pageText);
                        text.AppendLine();
                    }

                    var rawText = text.ToString();
                    return System.Text.RegularExpressions.Regex.Replace(rawText, @"[\p{C}-[\r\n\t]]+", " ").Trim();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract PDF text from {FilePath}", filePath);
                    return string.Empty;
                }
            });
        }

        public async Task<bool> DeletePdfAsync(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return false;

            await Task.CompletedTask;

            try
            {
                var normalized = NormalizeRelativePath(relativePath);
                var absolutePath = Path.Combine(_env.WebRootPath, normalized);

                if (!File.Exists(absolutePath))
                    return false;

                File.Delete(absolutePath);
                _logger.LogInformation("Deleted PDF file {Path}", absolutePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete PDF file {RelativePath}", relativePath);
                return false;
            }
        }

        public async Task<bool> DeletePdfAsync(int productId)
        {
            await Task.CompletedTask;

            try
            {
                var booksDir = Path.Combine(_env.WebRootPath, "pdfs", "books");
                if (!Directory.Exists(booksDir))
                    return false;

                var files = Directory.GetFiles(booksDir, $"{productId}_*.pdf");
                if (files.Length == 0)
                    return false;

                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                        _logger.LogInformation("Deleted PDF file {Path}", file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed deleting PDF file {Path}", file);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed deleting PDFs for product {ProductId}", productId);
                return false;
            }
        }

        private static bool LooksLikePdf(string contentType, byte[]? firstBytes)
        {
            var contentTypeLooksValid =
                contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) ||
                contentType.Contains("octet-stream", StringComparison.OrdinalIgnoreCase);

            var signatureLooksValid =
                firstBytes != null &&
                firstBytes.Length >= 4 &&
                firstBytes[0] == 0x25 &&
                firstBytes[1] == 0x50 &&
                firstBytes[2] == 0x44 &&
                firstBytes[3] == 0x46;

            return contentTypeLooksValid || signatureLooksValid;
        }

        private static string SanitizeToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "source";

            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(value.Where(c => !invalid.Contains(c)).ToArray());

            cleaned = cleaned.Replace(' ', '_');
            return string.IsNullOrWhiteSpace(cleaned) ? "source" : cleaned;
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            return relativePath
                .TrimStart('~', '/', '\\')
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
        }
    }
}