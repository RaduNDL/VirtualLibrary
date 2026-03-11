using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VirtualLibrary.Data;
using VirtualLibrary.Models;
using VirtualLibrary.Services;

namespace VirtualLibrary.Pages.MyBooks
{
    [Authorize]
    public class ReadBookModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly AudiobookService _audiobookService;
        private readonly IWebHostEnvironment _env;

        public ReadBookModel(
            AppDbContext context,
            AudiobookService audiobookService,
            IWebHostEnvironment env)
        {
            _context = context;
            _audiobookService = audiobookService;
            _env = env;
        }

        public Product Product { get; private set; } = null!;
        public Audiobook? Audiobook { get; private set; }
        public bool HasPurchased { get; private set; }
        public string? AudioSourceUrl { get; private set; }
        public string? PdfUrl { get; private set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            HasPurchased = await _context.OrderItems
                .Where(o => o.ProductId == id && o.Order.UserId == userId)
                .AnyAsync();

            if (!HasPurchased)
                return RedirectToPage("/Library/Index");

            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            Product = product;

            Audiobook = await _context.Audiobooks
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ProductId == id);

            if (Audiobook?.Status == AudiobookStatus.Completed && !string.IsNullOrWhiteSpace(Audiobook.AudioFilePath))
                AudioSourceUrl = Url.Page("/MyBooks/ReadBook", "Audio", new { id });

            if (!string.IsNullOrWhiteSpace(Product.PdfFilePath))
                PdfUrl = Url.Content($"~/{Product.PdfFilePath}");

            return Page();
        }

        public async Task<IActionResult> OnPostGenerateAudioAsync(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var hasPurchased = await _context.OrderItems
                .Include(o => o.Order)
                .AnyAsync(o => o.Order.UserId == userId && o.ProductId == id);

            if (!hasPurchased)
            {
                StatusMessage = "✗ You must purchase this book before generating an audiobook.";
                return RedirectToPage(new { id });
            }

            try
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
                if (product == null)
                {
                    StatusMessage = "✗ Product not found.";
                    return RedirectToPage(new { id });
                }

                var audiobook = await _audiobookService.GenerateAudiobookAsync(id);

                if (audiobook == null)
                {
                    StatusMessage = "✗ Audiobook generation failed.";
                }
                else if (audiobook.Status == AudiobookStatus.Completed)
                {
                    StatusMessage = "✓ Audiobook generated successfully.";
                }
                else if (audiobook.Status == AudiobookStatus.Processing)
                {
                    StatusMessage = "⏳ Audiobook is being generated.";
                }
                else if (audiobook.Status == AudiobookStatus.Failed)
                {
                    StatusMessage = $"✗ Generation failed: {audiobook.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"✗ Error: {ex.Message}";
            }

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnGetAudioAsync(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var hasPurchased = await _context.OrderItems
                .Include(o => o.Order)
                .AnyAsync(o => o.Order.UserId == userId && o.ProductId == id);

            if (!hasPurchased && !User.IsInRole("Administrator"))
                return Forbid();

            var audiobook = await _context.Audiobooks
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ProductId == id);

            if (audiobook == null ||
                audiobook.Status != AudiobookStatus.Completed ||
                string.IsNullOrWhiteSpace(audiobook.AudioFilePath))
            {
                return NotFound();
            }

            var absolutePath = Path.Combine(
                _env.ContentRootPath,
                audiobook.AudioFilePath.TrimStart('/', '\\')
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(absolutePath))
                return NotFound();

            var contentType = GetAudioContentType(absolutePath);

            return new PhysicalFileResult(absolutePath, contentType)
            {
                EnableRangeProcessing = true
            };
        }

        private static string GetAudioContentType(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();

            return ext switch
            {
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                _ => "application/octet-stream"
            };
        }
    }
}