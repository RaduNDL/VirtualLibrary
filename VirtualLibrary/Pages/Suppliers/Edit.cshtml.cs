using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Pages.Suppliers
{
    [Authorize(Roles = "Administrator")]
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;
        public EditModel(AppDbContext context) => _context = context;

        [BindProperty]
        public Supplier Supplier { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var s = await _context.Suppliers.FindAsync(id);
            if (s is null) return NotFound();
            Supplier = s;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var db = await _context.Suppliers.FirstOrDefaultAsync(x => x.SupplierId == Supplier.SupplierId);
            if (db is null) return NotFound();

            db.Name = Supplier.Name;
            db.ContactInfo = Supplier.ContactInfo;
            db.Address = Supplier.Address;

            await _context.SaveChangesAsync();
            return RedirectToPage("Index", new { status = "Supplier updated." });
        }
    }
}
