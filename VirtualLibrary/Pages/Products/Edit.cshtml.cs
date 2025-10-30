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
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public EditModel(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [BindProperty]
        public Product Product { get; set; } = null!;

        [BindProperty]
        [Display(Name = "Replace cover")]
        public IFormFile? ImageFile { get; set; }

        public SelectList? Categories { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id)
                ?? throw new InvalidOperationException("Product not found");

            Categories = new SelectList(
                await _context.Categories
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToListAsync(),
                nameof(Category.CategoryId), 
                nameof(Category.Name),
                Product.CategoryId
            );

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                Categories = new SelectList(
                    await _context.Categories
                        .AsNoTracking()
                        .OrderBy(c => c.Name)
                        .ToListAsync(),
                    nameof(Category.CategoryId),
                    nameof(Category.Name),
                    Product.CategoryId
                );
                return Page();
            }

            var dbProduct = await _context.Products.FirstOrDefaultAsync(p => p.Id == Product.Id);
            if (dbProduct is null) return NotFound();

            dbProduct.Title = Product.Title;
            dbProduct.Author = Product.Author;
            dbProduct.Isbn = Product.Isbn;
            dbProduct.Description = Product.Description;
            dbProduct.Price = Product.Price;
            dbProduct.Stock = Product.Stock;
            dbProduct.CategoryId = Product.CategoryId;
            dbProduct.UpdatedAtUtc = DateTime.UtcNow;

            if (ImageFile is not null && ImageFile.Length > 0)
            {
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var ext = Path.GetExtension(ImageFile.FileName).ToLowerInvariant();

                if (!allowed.Contains(ext))
                {
                    ModelState.AddModelError(nameof(ImageFile), "Supported format: .jpg, .jpeg, .png, .webp");
                    Categories = new SelectList(
                        await _context.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
                        nameof(Category.CategoryId),
                        nameof(Category.Name),
                        Product.CategoryId
                    );
                    return Page();
                }

                if (ImageFile.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError(nameof(ImageFile), "The file is too large. (max 5MB).");
                    Categories = new SelectList(
                        await _context.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
                        nameof(Category.CategoryId),
                        nameof(Category.Name),
                        Product.CategoryId
                    );
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

                if (!string.IsNullOrWhiteSpace(dbProduct.ImagePath))
                {
                    var oldFull = Path.Combine(_env.WebRootPath, dbProduct.ImagePath.Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(oldFull))
                        System.IO.File.Delete(oldFull);
                }

                dbProduct.ImagePath = Path.Combine("uploads", "books", fileName).Replace('\\', '/');
            }

            await _context.SaveChangesAsync();
            return RedirectToPage("./Index");
        }
    }
}
