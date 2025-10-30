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
        public DetailsModel(AppDbContext context) => _context = context;

        public Product Product { get; set; } = null!;

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public string SafeReturnUrl { get; private set; } = "/";

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Supplier) 
                .FirstOrDefaultAsync(m => m.Id == id);

            if (Product is null) return NotFound();

            var candidate = !string.IsNullOrWhiteSpace(ReturnUrl)
                ? ReturnUrl
                : Request.Headers["Referer"].ToString();

            if (!string.IsNullOrWhiteSpace(candidate) && Url.IsLocalUrl(candidate))
            {
                SafeReturnUrl = candidate!;
            }
            else
            {
                SafeReturnUrl = Url.Page("/Library/Index") ?? Url.Page("/Index") ?? "/";
            }

            return Page();
        }
    }
}
