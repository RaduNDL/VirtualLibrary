using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VirtualLibrary.Data;
using VirtualLibrary.Models;
using VirtualLibrary.Pages.Products;
using VirtualLibrary.Services;

namespace VirtualLibrary.Pages.MyBooks
{
    [Authorize]
    public class ReadBookModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly AudiobookService _audiobookService;

        public ReadBookModel(AppDbContext context, AudiobookService audiobookService)
        {
            _context = context;
            _audiobookService = audiobookService;
        }

        public Product Product { get; set; } = null!;
        public Audiobook? Audiobook { get; set; }
        public bool HasPurchased { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Check if user purchased this product
            HasPurchased = await _context.OrderItems
                .Include(oi => oi.Order)
                .AnyAsync(oi => oi.Order.UserId == userId && oi.ProductId == id);

            if (!HasPurchased)
            {
                return RedirectToPage("/Library/Index");
            }

            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            Product = product;

            Audiobook = await _context.Audiobooks
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ProductId == id);

            return Page();
        }

        public async Task<IActionResult> OnPostGenerateAudioAsync([FromBody] GenerateAudioRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Verify purchase
            var hasPurchased = await _context.OrderItems
                .Include(oi => oi.Order)
                .AnyAsync(oi => oi.Order.UserId == userId && oi.ProductId == request.ProductId);

            if (!hasPurchased)
            {
                return new JsonResult(new { error = "You must purchase this book first." }) { StatusCode = 403 };
            }

            try
            {
                var audiobook = await _audiobookService.GenerateAudiobookAsync(request.ProductId);

                if (audiobook == null)
                    return new JsonResult(new { error = "Product not found." }) { StatusCode = 404 };

                if (audiobook.HasFailed)
                    return new JsonResult(new { error = audiobook.ErrorMessage }) { StatusCode = 400 };

                if (audiobook.Status == AudiobookStatus.Rejected)
                    return new JsonResult(new { error = audiobook.ErrorMessage ?? "Content rejected for audio generation." }) { StatusCode = 400 };

                return new JsonResult(new
                {
                    audiobookId = audiobook.AudiobookId,
                    status = audiobook.Status.ToString(),
                    audioUrl = "/" + audiobook.AudioFilePath
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = "Server error: " + ex.Message }) { StatusCode = 500 };
            }
        }
    }
}