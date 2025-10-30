using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Pages.Categories
{
    [Authorize(Roles = "Admin,Administrator")]
    public class AddModel : PageModel
    {
        private readonly AppDbContext _context;
        public AddModel(AppDbContext context) => _context = context;

        [BindProperty]
        public Category Category { get; set; } = new();

        [TempData] public string? Message { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Category.Name))
                ModelState.AddModelError("Category.Name", "Name is required.");
                ModelState.Remove("Category.Products");

            if (!ModelState.IsValid)
                return Page();

            try
            {
                var name = Category.Name.Trim();
                var exists = await _context.Categories
                    .AsNoTracking()
                    .AnyAsync(c => c.Name.ToLower() == name.ToLower());

                if (exists)
                {
                    ModelState.AddModelError("Category.Name", "A category with this name already exists.");
                    return Page();
                }

                Category.Name = name;
                _context.Categories.Add(Category);
                var rows = await _context.SaveChangesAsync();

                if (rows > 0)
                {
                    Message = "Category created successfully.";
                    return RedirectToPage("Index");
                }

                ModelState.AddModelError(string.Empty, "No changes were saved. Please try again.");
                return Page();
            }
            catch (DbUpdateException ex)
            {
                ModelState.AddModelError(string.Empty, $"Database error: {ex.GetBaseException().Message}");
                return Page();
            }
        }
    }
}
