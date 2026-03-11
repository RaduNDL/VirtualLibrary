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

        public string SafeReturnUrl { get; private set; } = "/";

        public bool HasBookPdf => !string.IsNullOrWhiteSpace(Product?.PdfFilePath);
        public bool HasDescriptionPdf => !string.IsNullOrWhiteSpace(Product?.DescriptionPdfPath);

        public string? BookReaderUrl =>
            Product == null ? null : Url.Page("/Products/ReadPdf", new { id = Product.Id });

        public string? DescriptionReaderUrl =>
            Product == null ? null : Url.Page("/Products/DescriptionPdf", new { id = Product.Id });

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

            var candidate = !string.IsNullOrWhiteSpace(ReturnUrl)
                ? ReturnUrl
                : Request.Headers["Referer"].ToString();

            if (!string.IsNullOrWhiteSpace(candidate) && Url.IsLocalUrl(candidate))
            {
                SafeReturnUrl = candidate;
            }
            else
            {
                SafeReturnUrl = Url.Page("/Library/Index") ?? "/";
            }

            return Page();
        }
    }
}