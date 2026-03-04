using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Pages.Audiobooks
{
    [Authorize]
    public class PdfViewerModel(AppDbContext context) : PageModel
    {
        private readonly AppDbContext _context = context;

        public Product? Product { get; set; }
        public Audiobook? Audiobook { get; set; }

        public async Task<IActionResult> OnGetAsync(int productId)
        {
            Product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);

            if (Product == null)
            {
                return NotFound();
            }

            Audiobook = await _context.Audiobooks
                .FirstOrDefaultAsync(a => a.ProductId == productId && a.Status == "Completed");

            return Page();
        }
    }
}