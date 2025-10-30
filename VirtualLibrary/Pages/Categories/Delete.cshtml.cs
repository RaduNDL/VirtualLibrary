using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Pages.Categories
{
    [Authorize(Roles = "Admin,Administrator")]
    public class DeleteModel : PageModel
    {
        private readonly AppDbContext _context;
        public DeleteModel(AppDbContext context) => _context = context;

        [BindProperty]
        public Category Category { get; set; } = null!;

        [TempData]
        public string? Message { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Category = await _context.Categories.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CategoryId == id)
                ?? (Category)null!;

            if (Category == null) return NotFound();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            var cat = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryId == id);
            if (cat == null) return NotFound();

            var hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id);
            if (hasProducts)
            {
                Message = "Cannot delete category: there are products assigned to it.";
                return RedirectToPage("Index");
            }

            _context.Categories.Remove(cat);
            await _context.SaveChangesAsync();

            Message = "Category deleted successfully.";
            return RedirectToPage("Index");
        }
    }
}
