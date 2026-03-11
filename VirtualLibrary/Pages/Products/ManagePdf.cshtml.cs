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
    public class ManagePdfModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly PdfService _pdfService;
        private readonly BookPdfGenerator _bookPdfGenerator;
        private readonly ILogger<ManagePdfModel> _logger;

        public ManagePdfModel(
            AppDbContext context,
            PdfService pdfService,
            BookPdfGenerator bookPdfGenerator,
            ILogger<ManagePdfModel> logger)
        {
            _context = context;
            _pdfService = pdfService;
            _bookPdfGenerator = bookPdfGenerator;
            _logger = logger;
        }

        [BindProperty]
        public int ProductId { get; set; }

        public Product? Product { get; private set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public bool HasBookPdf => Product != null && !string.IsNullOrWhiteSpace(Product.PdfFilePath);
        public bool HasDescriptionPdf => Product != null && !string.IsNullOrWhiteSpace(Product.DescriptionPdfPath);

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Product = await LoadProductAsync(id);
            if (Product == null)
                return NotFound();

            ProductId = id;
            return Page();
        }

        public async Task<IActionResult> OnPostGenerateDescriptionAsync(int productId)
        {
            try
            {
                var product = await LoadProductAsync(productId);
                if (product == null)
                    return NotFound();

                if (!string.IsNullOrWhiteSpace(product.DescriptionPdfPath))
                    await _pdfService.DeletePdfAsync(product.DescriptionPdfPath);

                var generatedPath = await _bookPdfGenerator.GenerateBookDescriptionPdfAsync(product);
                if (string.IsNullOrWhiteSpace(generatedPath))
                {
                    StatusMessage = "Failed to generate the details PDF.";
                    return RedirectToPage(new { id = productId });
                }

                product.DescriptionPdfPath = generatedPath;
                product.UpdatedAtUtc = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                StatusMessage = "Details PDF generated successfully.";

                return RedirectToPage(new { id = productId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating details PDF for product {ProductId}", productId);
                StatusMessage = $"Error: {ex.Message}";
                return RedirectToPage(new { id = productId });
            }
        }

        public async Task<IActionResult> OnPostSearchOpenLibraryAsync(int productId)
        {
            try
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
                if (product == null)
                    return NotFound();

                var pdfUrl = await _pdfService.SearchOpenLibraryPdfAsync(
                    product.Isbn,
                    product.Title,
                    product.Author ?? string.Empty);

                if (string.IsNullOrWhiteSpace(pdfUrl))
                {
                    StatusMessage = "No public book PDF was found on Open Library for this title.";
                    return RedirectToPage(new { id = productId });
                }

                if (!string.IsNullOrWhiteSpace(product.PdfFilePath))
                    await _pdfService.DeletePdfAsync(product.PdfFilePath);

                var savedPath = await _pdfService.DownloadAndSavePdfAsync(productId, pdfUrl, "OpenLibrary");
                if (string.IsNullOrWhiteSpace(savedPath))
                {
                    StatusMessage = "A PDF was found, but the file could not be downloaded or validated.";
                    return RedirectToPage(new { id = productId });
                }

                product.PdfFilePath = savedPath;
                product.PdfSource = "OpenLibrary";
                product.UpdatedAtUtc = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                StatusMessage = "Book PDF downloaded successfully from Open Library.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Open Library for product {ProductId}", productId);
                StatusMessage = $"Error: {ex.Message}";
            }

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
                    StatusMessage = "Please select a PDF file.";
                    return RedirectToPage(new { id = productId });
                }

                if (!string.IsNullOrWhiteSpace(product.PdfFilePath))
                    await _pdfService.DeletePdfAsync(product.PdfFilePath);

                var savedPath = await _pdfService.UploadPdfAsync(productId, pdfFile);
                if (string.IsNullOrWhiteSpace(savedPath))
                {
                    StatusMessage = "Upload failed. Make sure the file is a real PDF and is under 100 MB.";
                    return RedirectToPage(new { id = productId });
                }

                product.PdfFilePath = savedPath;
                product.PdfSource = "Manual";
                product.UpdatedAtUtc = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                StatusMessage = "Book PDF uploaded successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading PDF for product {ProductId}", productId);
                StatusMessage = $"Error: {ex.Message}";
            }

            return RedirectToPage(new { id = productId });
        }

        public async Task<IActionResult> OnPostDownloadUrlAsync(int productId, string? pdfUrl)
        {
            try
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
                if (product == null)
                    return NotFound();

                if (string.IsNullOrWhiteSpace(pdfUrl) || !Uri.TryCreate(pdfUrl, UriKind.Absolute, out _))
                {
                    StatusMessage = "Please enter a valid absolute PDF URL.";
                    return RedirectToPage(new { id = productId });
                }

                if (!string.IsNullOrWhiteSpace(product.PdfFilePath))
                    await _pdfService.DeletePdfAsync(product.PdfFilePath);

                var savedPath = await _pdfService.DownloadAndSavePdfAsync(productId, pdfUrl.Trim(), "URL");
                if (string.IsNullOrWhiteSpace(savedPath))
                {
                    StatusMessage = "Failed to download a valid PDF from the provided URL.";
                    return RedirectToPage(new { id = productId });
                }

                product.PdfFilePath = savedPath;
                product.PdfSource = "URL";
                product.UpdatedAtUtc = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                StatusMessage = "Book PDF downloaded and saved successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading PDF from URL for product {ProductId}", productId);
                StatusMessage = $"Error: {ex.Message}";
            }

            return RedirectToPage(new { id = productId });
        }

        public async Task<IActionResult> OnPostDeleteAsync(int productId)
        {
            try
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
                if (product == null)
                    return NotFound();

                var deleted = false;

                if (!string.IsNullOrWhiteSpace(product.PdfFilePath))
                    deleted = await _pdfService.DeletePdfAsync(product.PdfFilePath);

                product.PdfFilePath = null;
                product.PdfSource = null;
                product.UpdatedAtUtc = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                StatusMessage = deleted ? "Book PDF deleted successfully." : "Book PDF record cleared.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting PDF for product {ProductId}", productId);
                StatusMessage = $"Error: {ex.Message}";
            }

            return RedirectToPage(new { id = productId });
        }

        private Task<Product?> LoadProductAsync(int id)
        {
            return _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.Id == id);
        }
    }
}