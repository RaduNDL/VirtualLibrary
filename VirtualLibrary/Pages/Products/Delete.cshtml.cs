using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Pages.Products
{
    [Authorize(Roles = "Administrator")]
    public class DeleteModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public DeleteModel(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [BindProperty]
        public Product Product { get; set; } = null!;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.Id == id) ?? throw new InvalidOperationException("Not found");

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            var prod = await _context.Products.FindAsync(id);
            if (prod == null) return NotFound();

            _context.Products.Remove(prod);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(prod.ImagePath))
            {
                var path = Path.Combine(_env.WebRootPath, prod.ImagePath.Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }

            return RedirectToPage("./Index");
        }
    }
}
