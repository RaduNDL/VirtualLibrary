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

namespace VirtualLibrary.Pages.Audiobooks
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

        public IList<Product> Products { get; set; } = new List<Product>();
        public Dictionary<int, Audiobook?> AudiobookStatus { get; set; } = new();
        public string CurrentUserId { get; set; } = "";
        public int TotalAudiobooksGenerated { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task OnGetAsync()
        {
            CurrentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            var purchasedProductIds = await _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.Order.UserId == CurrentUserId)
                .Select(oi => oi.ProductId)
                .Distinct()
                .ToListAsync();

            Products = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => purchasedProductIds.Contains(p.Id))
                .OrderByDescending(p => p.CreatedAtUtc)
                .ToListAsync();

            var audiobooks = await _context.Audiobooks
                .AsNoTracking()
                .Where(a => purchasedProductIds.Contains(a.ProductId))
                .ToListAsync();

            foreach (var product in Products)
            {
                AudiobookStatus[product.Id] = audiobooks.FirstOrDefault(a => a.ProductId == product.Id);
            }

            TotalAudiobooksGenerated = audiobooks.Count(a => a.Status == Models.AudiobookStatus.Completed);
        }

        public async Task<IActionResult> OnPostGenerateAsync(int productId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

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
                    StatusMessage = "✗ Product not found";
                    return RedirectToPage();
                }

                _logger.LogInformation("User generating audiobook for product {ProductId}", productId);
                var audiobook = await _audiobookService.GenerateAudiobookAsync(productId);

                if (audiobook?.Status == Models.AudiobookStatus.Completed)
                    StatusMessage = $"✓ Audiobook ready for '{product.Title}'! You can now download it.";
                else if (audiobook?.Status == Models.AudiobookStatus.Failed)
                    StatusMessage = $"✗ Failed to generate audiobook: {audiobook.ErrorMessage}";
                else if (audiobook?.Status == Models.AudiobookStatus.Failed)
                    StatusMessage = $"✗ Rejected: {audiobook.ErrorMessage}";
                else if (audiobook?.Status == Models.AudiobookStatus.Processing)
                    StatusMessage = $"⏳ Generating audiobook for '{product.Title}'... This may take a few minutes.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating audiobook");
                StatusMessage = $"✗ Error: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int audiobookId)
        {
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
}