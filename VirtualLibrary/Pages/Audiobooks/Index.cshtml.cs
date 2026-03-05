using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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

            Products = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .OrderByDescending(p => p.CreatedAtUtc)
                .ToListAsync();

            var audiobooks = await _context.Audiobooks
                .AsNoTracking()
                .ToListAsync();

            foreach (var product in Products)
            {
                AudiobookStatus[product.Id] = audiobooks.FirstOrDefault(a => a.ProductId == product.Id);
            }

            TotalAudiobooksGenerated = audiobooks.Count(a => a.Status == "Completed");
        }

        public async Task<IActionResult> OnPostGenerateAsync(int productId)
        {
            try
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
                if (product == null)
                {
                    StatusMessage = "✗ Product not found";
                    return RedirectToPage();
                }

                _logger.LogInformation($"User generating audiobook for product {productId}");
                var audiobook = await _audiobookService.GenerateAudiobookAsync(productId);

                if (audiobook?.Status == "Completed")
                    StatusMessage = $"✓ Audiobook ready for '{product.Title}'! You can now download it.";
                else if (audiobook?.Status == "Failed")
                    StatusMessage = $"✗ Failed to generate audiobook: {audiobook.ErrorMessage}";
                else if (audiobook?.Status == "Processing")
                    StatusMessage = $"⏳ Generating audiobook for '{product.Title}'... This may take a few minutes.";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                StatusMessage = $"✗ Error: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int audiobookId)
        {
            try
            {
                var audiobook = await _context.Audiobooks.FirstOrDefaultAsync(a => a.AudiobookId == audiobookId);

                if (audiobook == null)
                {
                    StatusMessage = "✗ Audiobook not found";
                    return RedirectToPage();
                }

                var success = await _audiobookService.DeleteAudiobookAsync(audiobookId);
                StatusMessage = success
                    ? "✓ Audiobook deleted successfully"
                    : "✗ Failed to delete audiobook";
            }
            catch (Exception ex)
            {
                StatusMessage = $"✗ Error: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnGetDownloadAsync(int audiobookId)
        {
            var audiobook = await _context.Audiobooks.FirstOrDefaultAsync(a => a.AudiobookId == audiobookId);

            if (audiobook == null || string.IsNullOrWhiteSpace(audiobook.AudioFilePath))
                return NotFound();

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", audiobook.AudioFilePath);

            if (!System.IO.File.Exists(fullPath))
                return NotFound();

            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == audiobook.ProductId);
            var fileName = $"{product?.Title?.Replace(" ", "_") ?? "audiobook"}.mp3";

            return File(fileBytes, "audio/mpeg", fileName);
        }
    }
}