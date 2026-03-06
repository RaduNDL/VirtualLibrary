using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Pages.Products
{
    [Authorize(Roles = "Administrator")]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public CreateModel(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [BindProperty]
        public Product Product { get; set; } = new();

        [BindProperty]
        [Display(Name = "Cover image")]
        public IFormFile? ImageFile { get; set; }

        public SelectList? Categories { get; set; }
        public SelectList? Suppliers { get; set; }

        public async Task OnGetAsync()
        {
            await LoadLookupsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadLookupsAsync(Product.CategoryId, Product.SupplierId);
                return Page();
            }

            if (ImageFile is not null && ImageFile.Length > 0)
            {
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var ext = Path.GetExtension(ImageFile.FileName).ToLowerInvariant();

                if (!allowed.Contains(ext))
                {
                    ModelState.AddModelError(nameof(ImageFile), "Supported format: .jpg, .jpeg, .png, .webp");
                    await LoadLookupsAsync(Product.CategoryId, Product.SupplierId);
                    return Page();
                }

                if (ImageFile.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError(nameof(ImageFile), "The file is too large. (max 5MB).");
                    await LoadLookupsAsync(Product.CategoryId, Product.SupplierId);
                    return Page();
                }

                var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", "books");
                Directory.CreateDirectory(uploadsRoot);

                var fileName = $"{Guid.NewGuid():N}{ext}";
                var fullPath = Path.Combine(uploadsRoot, fileName);

                using (var stream = System.IO.File.Create(fullPath))
                {
                    await ImageFile.CopyToAsync(stream);
                }

                Product.ImagePath = Path.Combine("uploads", "books", fileName).Replace('\\', '/');
            }

            _context.Products.Add(Product);
            await _context.SaveChangesAsync();
            return RedirectToPage("./Index");
        }

        private async Task LoadLookupsAsync(int categoryId, object supplierId)
        {
            throw new NotImplementedException();
        }

        private async Task LoadLookupsAsync(int? selectedCategoryId = null, int? selectedSupplierId = null)
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync();

            var suppliers = await _context.Suppliers
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .ToListAsync();

            Categories = new SelectList(categories, nameof(Category.CategoryId), nameof(Category.Name), selectedCategoryId);
            Suppliers = new SelectList(suppliers, nameof(Supplier.SupplierId), nameof(Supplier.Name), selectedSupplierId);
        }
    }
}
