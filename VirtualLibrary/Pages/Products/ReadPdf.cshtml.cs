using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Pages.Products
{
    // AllowAnonymous: oricine poate citi daca are PDF
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public class ReadPdfModel : PageModel
    {
        private readonly AppDbContext _context;

        public ReadPdfModel(AppDbContext context) => _context = context;

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
    }
}