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
        private readonly AudiobookQueue _queue;
        private readonly IWebHostEnvironment _env;

        public ReadBookModel(
            AppDbContext context,
            AudiobookService audiobookService,
            AudiobookQueue queue,
            IWebHostEnvironment env)
        {
            _context = context;
            _audiobookService = audiobookService;
            _queue = queue;
            _env = env;
        }

        public Product Product { get; set; } = null!;
        public Audiobook? Audiobook { get; set; }
        public bool HasPurchased { get; set; }
        public string? AudioSourceUrl { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            HasPurchased = await _context.OrderItems
                .Where(o => o.ProductId == id && o.Order.UserId == userId)
                .AnyAsync();

            if (!HasPurchased)
                return RedirectToPage("/Library/Index");

            Product = await _context.Products
                .AsNoTracking()
                .FirstAsync(p => p.Id == id);

            Audiobook = await _context.Audiobooks
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ProductId == id);

            if (Audiobook?.IsCompleted == true)
                AudioSourceUrl = $"/MyBooks/ReadBook?handler=Audio&id={id}";

            return Page();
        }

        public async Task<IActionResult> OnPostGenerateAudioAsync(int id)
        {
            var audiobook = await _audiobookService.StartGenerationAsync(id);

            if (audiobook == null)
                return new JsonResult(new { error = "Book not found" });

            if (audiobook.IsCompleted)
            {
                return new JsonResult(new
                {
                    status = "Completed",
                    audioUrl = $"/MyBooks/ReadBook?handler=Audio&id={id}"
                });
            }

            await _queue.EnqueueAsync(id);

            return new JsonResult(new
            {
                status = "Processing"
            });
        }

        public async Task<IActionResult> OnGetCheckStatusAsync(int id)
        {
            var audiobook = await _context.Audiobooks
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ProductId == id);

            if (audiobook == null)
                return new JsonResult(new { status = "NotFound" });

            return new JsonResult(new
            {
                status = audiobook.Status.ToString(),
                audioUrl = audiobook.IsCompleted
                    ? $"/MyBooks/ReadBook?handler=Audio&id={id}"
                    : null
            });
        }

        public async Task<IActionResult> OnGetAudioAsync(int id)
        {
            var audiobook = await _context.Audiobooks
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ProductId == id);

            if (audiobook == null || !audiobook.IsCompleted)
                return NotFound();

            var path = Path.Combine(_env.ContentRootPath, audiobook.AudioFilePath!);

            if (!System.IO.File.Exists(path))
                return NotFound();

            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            return File(stream, "audio/mpeg");
        }
    }
}