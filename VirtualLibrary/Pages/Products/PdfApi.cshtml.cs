using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;
using VirtualLibrary.Services;

namespace VirtualLibrary.Pages.Products
{
    [Authorize(Roles = "Administrator")]
    [IgnoreAntiforgeryToken]
    public class PdfApiModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly PdfService _pdfService;
        private readonly ILogger<PdfApiModel> _logger;

        public PdfApiModel(AppDbContext context, PdfService pdfService, ILogger<PdfApiModel> logger)
        {
            _context = context;
            _pdfService = pdfService;
            _logger = logger;
        }

        [AllowAnonymous]
        public async Task<IActionResult> OnGetStatusAsync(int productId)
        {
            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
                return new JsonResult(new { error = $"Product with id={productId} does not exist." })
                { StatusCode = 404 };

            return new JsonResult(new
            {
                productId = product.Id,
                title = product.Title,
                hasPdf = product.HasPdfAvailable,
                pdfPath = product.PdfFilePath,
                pdfSource = product.PdfSource
            });
        }

        [AllowAnonymous]
        [RequestSizeLimit(104_857_600)]
        public async Task<IActionResult> OnPostAutoCreateFromPdfAsync(IFormFile? pdfFile)
        {
            if (pdfFile == null || pdfFile.Length == 0)
                return new JsonResult(new { error = "No file provided." }) { StatusCode = 400 };

            var tempPath = Path.GetTempFileName();
            using (var stream = System.IO.File.Create(tempPath))
            {
                await pdfFile.CopyToAsync(stream);
            }

            var extractedText = await _pdfService.ExtractTextAsync(tempPath);
            var title = Path.GetFileNameWithoutExtension(pdfFile.FileName);

            var product = new Product
            {
                Title = title.Length > 200 ? title.Substring(0, 200) : title,
                Author = "Unknown",
                Description = extractedText.Length > 4000 ? extractedText.Substring(0, 4000) : extractedText,
                Price = 0.0m,
                Stock = 1,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            var savedPath = await _pdfService.UploadPdfAsync(product.Id, pdfFile);
            product.PdfFilePath = savedPath;
            product.PdfSource = "API-AutoCreate";
            await _context.SaveChangesAsync();

            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);

            return new JsonResult(new
            {
                success = true,
                productId = product.Id,
                title = product.Title,
                pdfPath = savedPath
            });
        }

        [RequestSizeLimit(104_857_600)]
        public async Task<IActionResult> OnPostUploadFileAsync(int productId, IFormFile? pdfFile)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null)
                return new JsonResult(new { error = $"Product with id={productId} does not exist." })
                { StatusCode = 404 };

            if (pdfFile == null || pdfFile.Length == 0)
                return new JsonResult(new { error = "No file sent. Use 'pdfFile' field." })
                { StatusCode = 400 };

            var savedPath = await _pdfService.UploadPdfAsync(productId, pdfFile);
            if (string.IsNullOrEmpty(savedPath))
                return new JsonResult(new { error = "Upload failed. Ensure file is a valid PDF and max 100 MB." })
                { StatusCode = 400 };

            product.PdfFilePath = savedPath;
            product.PdfSource = "API-Upload";
            await _context.SaveChangesAsync();

            _logger.LogInformation("PDF uploaded via PdfApi for product {Id}", productId);

            return new JsonResult(new
            {
                success = true,
                productId,
                pdfPath = savedPath,
                message = $"PDF uploaded successfully for '{product.Title}'."
            });
        }

        public async Task<IActionResult> OnPostUploadUrlAsync([FromBody] UploadUrlInput input)
        {
            if (string.IsNullOrWhiteSpace(input?.Url))
                return new JsonResult(new { error = "Field 'url' is required." })
                { StatusCode = 400 };

            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == input.ProductId);
            if (product == null)
                return new JsonResult(new { error = $"Product with id={input.ProductId} does not exist." })
                { StatusCode = 404 };

            var savedPath = await _pdfService.DownloadAndSavePdfAsync(input.ProductId, input.Url, "API-URL");
            if (string.IsNullOrEmpty(savedPath))
                return new JsonResult(new { error = "Could not download PDF. Check URL." })
                { StatusCode = 400 };

            product.PdfFilePath = savedPath;
            product.PdfSource = "API-URL";
            await _context.SaveChangesAsync();

            return new JsonResult(new
            {
                success = true,
                productId = input.ProductId,
                pdfPath = savedPath,
                sourceUrl = input.Url,
                message = $"PDF downloaded and saved for '{product.Title}'."
            });
        }

        public async Task<IActionResult> OnPostSearchOpenLibraryAsync([FromBody] SearchInput input)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == input.ProductId);
            if (product == null)
                return new JsonResult(new { error = $"Product with id={input.ProductId} does not exist." })
                { StatusCode = 404 };

            var pdfUrl = await _pdfService.SearchOpenLibraryPdfAsync(
                product.Isbn, product.Title, product.Author ?? "");

            if (string.IsNullOrEmpty(pdfUrl))
                return new JsonResult(new { error = "No PDF found on Open Library." })
                { StatusCode = 404 };

            var savedPath = await _pdfService.DownloadAndSavePdfAsync(input.ProductId, pdfUrl, "OpenLibrary");
            if (string.IsNullOrEmpty(savedPath))
                return new JsonResult(new { error = "PDF found but download failed." })
                { StatusCode = 400 };

            product.PdfFilePath = savedPath;
            product.PdfSource = "OpenLibrary";
            await _context.SaveChangesAsync();

            return new JsonResult(new
            {
                success = true,
                productId = input.ProductId,
                pdfPath = savedPath,
                sourceUrl = pdfUrl,
                message = $"PDF found automatically from Open Library for '{product.Title}'."
            });
        }

        public async Task<IActionResult> OnPostSearchAllAsync([FromBody] SearchAllInput? input)
        {
            int maxBooks = input?.MaxBooks ?? 10;
            if (maxBooks < 1 || maxBooks > 100) maxBooks = 10;

            var productsWithoutPdf = await _context.Products
                .Where(p => string.IsNullOrEmpty(p.PdfFilePath))
                .Take(maxBooks)
                .ToListAsync();

            if (!productsWithoutPdf.Any())
                return new JsonResult(new { message = "All books already have PDFs!", processed = 0 });

            var results = new List<object>();
            foreach (var product in productsWithoutPdf)
            {
                try
                {
                    var pdfUrl = await _pdfService.SearchOpenLibraryPdfAsync(
                        product.Isbn, product.Title, product.Author ?? "");

                    if (string.IsNullOrEmpty(pdfUrl))
                    {
                        results.Add(new { productId = product.Id, title = product.Title, status = "not_found" });
                        continue;
                    }

                    var savedPath = await _pdfService.DownloadAndSavePdfAsync(product.Id, pdfUrl, "OpenLibrary");
                    if (!string.IsNullOrEmpty(savedPath))
                    {
                        product.PdfFilePath = savedPath;
                        product.PdfSource = "OpenLibrary";
                        results.Add(new { productId = product.Id, title = product.Title, status = "ok", pdfPath = savedPath });
                    }
                    else
                    {
                        results.Add(new { productId = product.Id, title = product.Title, status = "download_failed" });
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new { productId = product.Id, title = product.Title, status = "error", error = ex.Message });
                }

                await Task.Delay(500);
            }

            await _context.SaveChangesAsync();

            return new JsonResult(new { processed = productsWithoutPdf.Count, results });
        }
    }

    public class UploadUrlInput { public int ProductId { get; set; } public string? Url { get; set; } }
    public class SearchInput { public int ProductId { get; set; } }
    public class SearchAllInput { public int MaxBooks { get; set; } = 10; }
}