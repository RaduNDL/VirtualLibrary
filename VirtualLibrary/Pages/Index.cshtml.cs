using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Pages
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public List<Product> FeaturedProducts { get; set; }

        public void OnGet()
        {
            FeaturedProducts = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .OrderByDescending(p => p.Price)
                .Take(4)
                .ToList();
        }
    }
}
