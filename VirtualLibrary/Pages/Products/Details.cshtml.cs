using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;
using VirtualLibrary.Services;

namespace VirtualLibrary.Pages.Products
{
    [AllowAnonymous]
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ProductDiscoveryService _discovery;

        public DetailsModel(AppDbContext context, ProductDiscoveryService discovery)
        {
            _context = context;
            _discovery = discovery;
        }

        public Product Product { get; private set; } = null!;
        public IList<SimilarProductResult> SimilarProducts { get; private set; } = new List<SimilarProductResult>();

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public string SafeReturnUrl { get; private set; } = "/";

        public bool HasBookPdf => !string.IsNullOrWhiteSpace(Product.BookPdfPath);
        public bool HasDescriptionPdf => !string.IsNullOrWhiteSpace(Product.DescriptionPdfPath);

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            Product = product;
            SimilarProducts = await _discovery.GetSimilarProductsAsync(id, 6);

            var candidate = !string.IsNullOrWhiteSpace(ReturnUrl)
                ? ReturnUrl
                : Request.Headers["Referer"].ToString();

            SafeReturnUrl = !string.IsNullOrWhiteSpace(candidate) && Url.IsLocalUrl(candidate)
                ? candidate
                : Url.Page("/Library/Index") ?? "/";

            return Page();
        }
    }
}