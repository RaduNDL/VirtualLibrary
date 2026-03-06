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

            TotalAudiobooksGenerated = audiobooks.Count(a => a.Status == Models.AudiobookStatus.Completed);
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

                _logger.LogInformation("User generating audiobook for product {ProductId}", productId);
                var audiobook = await _audiobookService.GenerateAudiobookAsync(productId);

                if (audiobook?.Status == Models.AudiobookStatus.Completed)
                    StatusMessage = $"✓ Audiobook ready for '{product.Title}'! You can now download it.";
                else if (audiobook?.Status == Models.AudiobookStatus.Failed)
                    StatusMessage = $"✗ Failed to generate audiobook: {audiobook.ErrorMessage}";
                else if (audiobook?.Status == Models.AudiobookStatus.Rejected)
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
    }
}