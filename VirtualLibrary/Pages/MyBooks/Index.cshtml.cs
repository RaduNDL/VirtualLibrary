using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;
using VirtualLibrary.Services;

namespace VirtualLibrary.Pages.MyBooks
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly AudiobookService _audiobookService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            AppDbContext context,
            AudiobookService audiobookService,
            ILogger<IndexModel> logger)
        {
            _context = context;
            _audiobookService = audiobookService;
            _logger = logger;
        }

        public IList<PurchasedBookViewModel> PurchasedBooks { get; set; } = new List<PurchasedBookViewModel>();

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Get all distinct products this user has purchased
            var purchasedProductIds = await _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.Order.UserId == userId)
                .Select(oi => oi.ProductId)
                .Distinct()
                .ToListAsync();

            var products = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => purchasedProductIds.Contains(p.Id))
                .OrderByDescending(p => p.CreatedAtUtc)
                .ToListAsync();

            var audiobooks = await _context.Audiobooks
                .AsNoTracking()
                .Where(a => purchasedProductIds.Contains(a.ProductId))
                .ToListAsync();

            // Get order dates for each product
            var orderDates = await _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.Order.UserId == userId)
                .GroupBy(oi => oi.ProductId)
                .Select(g => new { ProductId = g.Key, PurchaseDate = g.Max(oi => oi.Order.OrderDate) })
                .ToDictionaryAsync(x => x.ProductId, x => x.PurchaseDate);

            foreach (var product in products)
            {
                var audiobook = audiobooks.FirstOrDefault(a => a.ProductId == product.Id);
                PurchasedBooks.Add(new PurchasedBookViewModel
                {
                    Product = product,
                    Audiobook = audiobook,
                    PurchaseDate = orderDates.ContainsKey(product.Id) ? orderDates[product.Id] : DateTime.MinValue
                });
            }
        }

        public async Task<IActionResult> OnPostGenerateAudiobookAsync(int productId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Verify the user has purchased this product
            var hasPurchased = await _context.OrderItems
                .Include(oi => oi.Order)
                .AnyAsync(oi => oi.Order.UserId == userId && oi.ProductId == productId);

            if (!hasPurchased)
            {
                StatusMessage = "✗ You must purchase this book before generating an audiobook.";
                return RedirectToPage();
            }

            try
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
                if (product == null)
                {
                    StatusMessage = "✗ Product not found.";
                    return RedirectToPage();
                }

                _logger.LogInformation("User {UserId} generating audiobook for purchased product {ProductId}", userId, productId);
                var audiobook = await _audiobookService.GenerateAudiobookAsync(productId);

                if (audiobook?.Status == AudiobookStatus.Completed)
                    StatusMessage = $"✓ Audiobook ready for '{product.Title}'! You can now listen or download it.";
                else if (audiobook?.Status == AudiobookStatus.Failed)
                    StatusMessage = $"✗ Failed to generate audiobook: {audiobook.ErrorMessage}";
                else if (audiobook?.Status == AudiobookStatus.Failed)
                    StatusMessage = $"✗ Rejected: {audiobook.ErrorMessage}";
                else if (audiobook?.Status == AudiobookStatus.Processing)
                    StatusMessage = $"⏳ Generating audiobook for '{product.Title}'... This may take a few minutes.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating audiobook for product {ProductId}", productId);
                StatusMessage = $"✗ Error: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAudiobookAsync(int audiobookId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Verify ownership
            var audiobook = await _context.Audiobooks
                .Include(a => a.Product)
                .FirstOrDefaultAsync(a => a.AudiobookId == audiobookId);

            if (audiobook == null)
            {
                StatusMessage = "✗ Audiobook not found.";
                return RedirectToPage();
            }

            var hasPurchased = await _context.OrderItems
                .Include(oi => oi.Order)
                .AnyAsync(oi => oi.Order.UserId == userId && oi.ProductId == audiobook.ProductId);

            if (!hasPurchased)
            {
                StatusMessage = "✗ You don't have access to this audiobook.";
                return RedirectToPage();
            }

            try
            {
                var success = await _audiobookService.DeleteAudiobookAsync(audiobookId);
                StatusMessage = success
                    ? "✓ Audiobook deleted successfully."
                    : "✗ Failed to delete audiobook.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting audiobook {AudiobookId}", audiobookId);
                StatusMessage = $"✗ Error: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetDownloadAsync(int audiobookId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var audiobook = await _context.Audiobooks
                .Include(a => a.Product)
                .FirstOrDefaultAsync(a => a.AudiobookId == audiobookId);

            if (audiobook == null || string.IsNullOrWhiteSpace(audiobook.AudioFilePath))
                return NotFound();

            var hasPurchased = await _context.OrderItems
                .Include(oi => oi.Order)
                .AnyAsync(oi => oi.Order.UserId == userId && oi.ProductId == audiobook.ProductId);

            if (!hasPurchased)
                return Forbid();

            var webRoot = ((IWebHostEnvironment)HttpContext.RequestServices.GetService(typeof(IWebHostEnvironment))!).WebRootPath;
            var filePath = Path.Combine(webRoot, audiobook.AudioFilePath);

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var fileName = $"{audiobook.Product?.Title ?? "audiobook"}.mp3";
            return PhysicalFile(filePath, "audio/mpeg", fileName);
        }
    }

    public class PurchasedBookViewModel
    {
        public Product Product { get; set; } = null!;
        public Audiobook? Audiobook { get; set; }
        public DateTime PurchaseDate { get; set; }
    }
}