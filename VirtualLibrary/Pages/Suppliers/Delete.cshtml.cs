using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Pages.Suppliers
{
    [Authorize(Roles = "Administrator")]
    public class DeleteModel : PageModel
    {
        private readonly AppDbContext _context;

        public DeleteModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Supplier? Supplier { get; set; }

        public string? ErrorMessage { get; set; }

        public int ProductCount { get; private set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id is null)
                return NotFound();

            Supplier = await _context.Suppliers
                .Include(s => s.Products)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SupplierId == id);

            if (Supplier is null)
                return NotFound();

            ProductCount = Supplier.Products?.Count ?? 0;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id is null)
                return NotFound();

            var supplier = await _context.Suppliers
                .Include(s => s.Products)
                .FirstOrDefaultAsync(s => s.SupplierId == id);

            if (supplier is null)
                return NotFound();

            var count = supplier.Products?.Count ?? 0;

            if (count > 0)
            {
                ErrorMessage = $"You cannot delete this provider; it has {count} associated products. " +
                               "Delete or reassign those products first.";
                Supplier = supplier;
                ProductCount = count;
                return Page();
            }

            _context.Suppliers.Remove(supplier);

            try
            {
                await _context.SaveChangesAsync();
                TempData["StatusMessage"] = "The provider has been deleted.";
                return RedirectToPage("./Index");
            }
            catch (DbUpdateException)
            {
                ErrorMessage = "The provider could not be deleted due to a database error.";
                Supplier = supplier;
                ProductCount = count;
                return Page();
            }
        }
    }
}
