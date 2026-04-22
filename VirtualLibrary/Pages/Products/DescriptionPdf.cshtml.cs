using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Pages.Products
{
    [AllowAnonymous]
    public class DescriptionPdfModel : PageModel
    {
        private readonly AppDbContext _context;

        public DescriptionPdfModel(AppDbContext context)
        {
            _context = context;
        }

        public Product Product { get; private set; } = null!;
        public string PdfUrl { get; private set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(product.DescriptionPdfPath))
                return NotFound();

            Product = product;
            PdfUrl = "/" + product.DescriptionPdfPath.TrimStart('/');

            return Page();
        }
    }
}