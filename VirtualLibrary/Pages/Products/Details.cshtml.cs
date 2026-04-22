using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Pages.Products
{
    [AllowAnonymous]
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;

        public DetailsModel(AppDbContext context)
        {
            _context = context;
        }

        public Product Product { get; private set; } = null!;

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public string SafeReturnUrl { get; private set; } = "/Library/Index";

        public bool HasDescriptionPdf => !string.IsNullOrWhiteSpace(Product.DescriptionPdfPath);

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            Product = product;

            if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                SafeReturnUrl = ReturnUrl!;

            return Page();
        }
    }
}