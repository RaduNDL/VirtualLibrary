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
    public class PdfViewerModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly PdfService _pdfService;

        public PdfViewerModel(AppDbContext context, IWebHostEnvironment env, PdfService pdfService)
        {
            _context = context;
            _env = env;
            _pdfService = pdfService;
        }

        public Product? Product { get; set; }
        public Audiobook? Audiobook { get; set; }
        public string? ExtractedText { get; set; }

        public async Task<IActionResult> OnGetAsync(int productId)
        {
            Product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);

            if (Product == null)
                return NotFound();

            Audiobook = await _context.Audiobooks
                .FirstOrDefaultAsync(a => a.ProductId == productId && a.Status == AudiobookStatus.Completed);

            if (!string.IsNullOrWhiteSpace(Product.PdfFilePath))
            {
                var absolutePath = Path.Combine(_env.WebRootPath, Product.PdfFilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                ExtractedText = await _pdfService.ExtractTextAsync(absolutePath);
            }

            return Page();
        }
    }
}