using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Pages.Suppliers
{
    [Authorize(Roles = "Administrator")]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;
        public CreateModel(AppDbContext context) => _context = context;

        [BindProperty]
        public Supplier Supplier { get; set; } = new();

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            bool exists = _context.Suppliers.Any(s => s.Name.ToLower() == Supplier.Name.ToLower());
            if (exists)
            {
                ModelState.AddModelError("Supplier.Name", "A supplier with this name already exists.");
                return Page();
            }

            _context.Suppliers.Add(Supplier);
            await _context.SaveChangesAsync();
            return RedirectToPage("Index", new { status = "Supplier created." });
        }
    }
}