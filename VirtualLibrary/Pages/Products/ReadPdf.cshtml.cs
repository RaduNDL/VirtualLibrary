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
    public class ReadPdfModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly AudiobookService _audiobookService;

        public ReadPdfModel(AppDbContext context, AudiobookService audiobookService)
        {
            _context = context;
            _audiobookService = audiobookService;
        }

        public Product Product { get; set; } = null!;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product is null)
                return NotFound();

            Product = product;
            return Page();
        }

        public async Task<IActionResult> OnPostGenerateAudioAsync([FromBody] GenerateAudioRequest request)
        {
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
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }
    }

    public class GenerateAudioRequest
    {
        public int ProductId { get; set; }
    }
}