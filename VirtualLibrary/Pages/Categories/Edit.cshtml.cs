using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Pages.Categories
{
    [Authorize(Roles = "Admin,Administrator")]
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;
        public EditModel(AppDbContext context) => _context = context;

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

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var dbCat = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == Category.CategoryId);

            if (dbCat == null) return NotFound();

            var newName = (Category.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                ModelState.AddModelError("Category.Name", "Name is required.");
                return Page();
            }

            var exists = await _context.Categories.AsNoTracking().AnyAsync(c =>
                c.CategoryId != Category.CategoryId &&
                c.Name.ToLower() == newName.ToLower());

            if (exists)
            {
                ModelState.AddModelError("Category.Name", "A category with this name already exists.");
                return Page();
            }

            dbCat.Name = newName;
            await _context.SaveChangesAsync();

            Message = "Category updated successfully.";
            return RedirectToPage("Index");
        }
    }
}
