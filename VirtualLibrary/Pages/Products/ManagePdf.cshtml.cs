using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;
using VirtualLibrary.Services;

namespace VirtualLibrary.Pages.Products
{
    [Authorize]
    public class ManagePdfModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly PdfService _pdfService;
        private readonly ILogger<ManagePdfModel> _logger;

        public ManagePdfModel(
            AppDbContext context,
            PdfService pdfService,
            ILogger<ManagePdfModel> logger)
        {
            _context = context;
            _pdfService = pdfService;
            _logger = logger;
        }

        [BindProperty]
        public int ProductId { get; set; }

        public Product? Product { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);

            if (Product == null)
                return NotFound();

            ProductId = id;
            return Page();
        }

        public async Task<IActionResult> OnPostSearchOpenLibraryAsync(int productId)
        {
            try
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
                if (product == null)
                    return NotFound();

                StatusMessage = "⏳ Searching Open Library...";

                var pdfUrl = await _pdfService.SearchOpenLibraryPdfAsync(
                    product.Isbn,
                    product.Title,
                    product.Author ?? "");

                if (string.IsNullOrEmpty(pdfUrl))
                {
                    StatusMessage = "✗ No PDF found in Open Library for this book.";
                }
                else
                {
                    var savedPath = await _pdfService.DownloadAndSavePdfAsync(
                        productId,
                        pdfUrl,
                        "OpenLibrary");

                    if (!string.IsNullOrEmpty(savedPath))
                    {
                        product.PdfFilePath = savedPath;
                        product.PdfSource = "OpenLibrary";
                        await _context.SaveChangesAsync();
                        StatusMessage = "✓ PDF found and downloaded successfully from Open Library!";
                    }
                    else
                    {
                        StatusMessage = "✗ PDF found but failed to download.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                StatusMessage = $"✗ Error: {ex.Message}";
            }

            ProductId = productId;
            return RedirectToPage(new { id = productId });
        }

        public async Task<IActionResult> OnPostUploadAsync(int productId, IFormFile? pdfFile)
        {
            try
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
                if (product == null)
                    return NotFound();

                if (pdfFile == null)
                {
                    StatusMessage = "✗ Please select a PDF file.";
                    ProductId = productId;
                    return RedirectToPage(new { id = productId });
                }

                var savedPath = await _pdfService.UploadPdfAsync(productId, pdfFile);

                if (string.IsNullOrEmpty(savedPath))
                {
                    StatusMessage = "✗ Failed to upload PDF. Make sure it's a valid PDF file (max 100 MB).";
                }
                else
                {
                    product.PdfFilePath = savedPath;
                    product.PdfSource = "Manual";
                    await _context.SaveChangesAsync();
                    StatusMessage = "✓ PDF uploaded successfully!";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                StatusMessage = $"✗ Error: {ex.Message}";
            }

            ProductId = productId;
            return RedirectToPage(new { id = productId });
        }

        public async Task<IActionResult> OnPostDeleteAsync(int productId)
        {
            try
            {
                var success = await _pdfService.DeletePdfAsync(productId);
                StatusMessage = success
                    ? "✓ PDF deleted successfully."
                    : "✗ Failed to delete PDF.";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                StatusMessage = $"✗ Error: {ex.Message}";
            }

            ProductId = productId;
            return RedirectToPage(new { id = productId });
        }
    }
}