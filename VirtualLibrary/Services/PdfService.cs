using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Services
{
    public class PdfService
    {
        private readonly HttpClient _httpClient;
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<PdfService> _logger;

        public PdfService(
            HttpClient httpClient,
            AppDbContext db,
            IWebHostEnvironment env,
            ILogger<PdfService> logger)
        {
            _httpClient = httpClient;
            _db = db;
            _env = env;
            _logger = logger;
        }

        public Task<string> ExtractTextAsync(string absolutePdfPath)
        {
            if (!File.Exists(absolutePdfPath))
                return Task.FromResult(string.Empty);

            try
            {
                var sb = new StringBuilder();
                using var doc = PdfDocument.Open(absolutePdfPath);
                foreach (var page in doc.GetPages())
                    sb.AppendLine(page.Text);

                return Task.FromResult(sb.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error extracting PDF text: {ex.Message}");
                return Task.FromResult(string.Empty);
            }
        }

        public async Task<string?> SearchOpenLibraryPdfAsync(string? isbn, string title, string author)
        {
            try
            {
                string searchQuery = !string.IsNullOrWhiteSpace(isbn)
                    ? $"isbn:{isbn}"
                    : $"title:{Uri.EscapeDataString(title)}+author:{Uri.EscapeDataString(author)}";

                var url = $"https://openlibrary.org/search.json?{searchQuery}&limit=5";

                using var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return null;

                using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);

                var root = doc.RootElement;
                if (!root.TryGetProperty("docs", out var docs) || docs.GetArrayLength() == 0)
                    return null;

                var firstBook = docs[0];

                if (firstBook.TryGetProperty("has_fulltext", out var hasFulltext) && hasFulltext.GetBoolean())
                {
                    if (firstBook.TryGetProperty("key", out var key))
                    {
                        var bookKey = key.GetString();
                        return $"https://openlibrary.org{bookKey}/pdf";
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error searching Open Library: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> DownloadAndSavePdfAsync(int productId, string pdfUrl, string source)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pdfUrl))
                    return null;

                using var response = await _httpClient.GetAsync(pdfUrl, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to download PDF: {response.StatusCode}");
                    return null;
                }

                if (response.Content.Headers.ContentLength.HasValue &&
                    response.Content.Headers.ContentLength > 100 * 1024 * 1024)
                {
                    _logger.LogWarning($"PDF too large: {response.Content.Headers.ContentLength}");
                    return null;
                }

                var pdfsDir = Path.Combine(_env.WebRootPath, "pdfs");
                if (!Directory.Exists(pdfsDir))
                    Directory.CreateDirectory(pdfsDir);

                var fileName = $"product_{productId}_{Guid.NewGuid():N}.pdf";
                var filePath = Path.Combine(pdfsDir, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    await response.Content.CopyToAsync(fileStream);
                }

                _logger.LogInformation($"PDF saved: {filePath}");
                return Path.Combine("pdfs", fileName).Replace('\\', '/');
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error downloading PDF: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> UploadPdfAsync(int productId, IFormFile pdfFile)
        {
            try
            {
                if (pdfFile == null || pdfFile.Length == 0)
                {
                    _logger.LogWarning("Upload attempted with null or empty file");
                    return null;
                }

                var allowedContentTypes = new[]
                {
                    "application/pdf",
                    "application/octet-stream",
                    "application/force-download",
                    "binary/octet-stream"
                };

                var extension = Path.GetExtension(pdfFile.FileName)?.ToLowerInvariant();

                if (extension != ".pdf")
                {
                    _logger.LogWarning($"Invalid file extension: {extension}");
                    return null;
                }

                if (!allowedContentTypes.Contains(pdfFile.ContentType?.ToLowerInvariant()))
                {
                    _logger.LogWarning($"Invalid content type: {pdfFile.ContentType}");
                    return null;
                }

                if (pdfFile.Length > 100 * 1024 * 1024)
                {
                    _logger.LogWarning($"PDF file too large: {pdfFile.Length} bytes");
                    return null;
                }

                var pdfsDir = Path.Combine(_env.WebRootPath, "pdfs");
                if (!Directory.Exists(pdfsDir))
                    Directory.CreateDirectory(pdfsDir);

                var fileName = $"product_{productId}_{Guid.NewGuid():N}.pdf";
                var filePath = Path.Combine(pdfsDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await pdfFile.CopyToAsync(stream);
                }

                _logger.LogInformation($"PDF uploaded successfully: {filePath}");
                return Path.Combine("pdfs", fileName).Replace('\\', '/');
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading PDF: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> DeletePdfAsync(int productId)
        {
            try
            {
                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId);

                if (product == null || string.IsNullOrWhiteSpace(product.PdfFilePath))
                    return false;

                var fullPath = Path.Combine(_env.WebRootPath, product.PdfFilePath);

                if (File.Exists(fullPath))
                    File.Delete(fullPath);

                product.PdfFilePath = null;
                product.PdfSource = null;
                await _db.SaveChangesAsync();

                _logger.LogInformation($"PDF deleted for product {productId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting PDF: {ex.Message}");
                return false;
            }
        }
    }
}