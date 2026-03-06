
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Net.Http;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using UglyToad.PdfPig;

namespace VirtualLibrary.Services
{
    public class PdfService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IHttpClientFactory _httpClientFactory;

        public PdfService(IWebHostEnvironment env, IHttpClientFactory httpClientFactory)
        {
            _env = env;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<string> SearchOpenLibraryPdfAsync(string? isbn, string title, string author)
        {
            var client = _httpClientFactory.CreateClient("PdfClient");
            string query = !string.IsNullOrEmpty(isbn)
                ? $"isbn={isbn}"
                : $"title={Uri.EscapeDataString(title)}&author={Uri.EscapeDataString(author)}";

            try
            {
                var response = await client.GetAsync($"https://openlibrary.org/search.json?{query}");
                if (!response.IsSuccessStatusCode) return string.Empty;

                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                if (root.GetProperty("numFound").GetInt32() > 0)
                {
                    var firstDoc = root.GetProperty("docs")[0];
                    if (firstDoc.TryGetProperty("ia", out var iaArray) && iaArray.GetArrayLength() > 0)
                    {
                        var identifier = iaArray[0].GetString();
                        return $"https://archive.org/download/{identifier}/{identifier}.pdf";
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        public async Task<string> DownloadAndSavePdfAsync(int productId, string url, string source)
        {
            var client = _httpClientFactory.CreateClient("PdfClient");
            try
            {
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) return string.Empty;

                var pdfDir = Path.Combine(_env.WebRootPath, "pdfs");
                if (!Directory.Exists(pdfDir)) Directory.CreateDirectory(pdfDir);

                var fileName = $"{productId}_auto_{Guid.NewGuid():N}.pdf";
                var filePath = Path.Combine(pdfDir, fileName);

                var data = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(filePath, data);

                return Path.Combine("pdfs", fileName).Replace('\\', '/');
            }
            catch { return string.Empty; }
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

        internal async Task<bool> DeletePdfAsync(int productId)
        {
            throw new NotImplementedException();
        }
    }
}