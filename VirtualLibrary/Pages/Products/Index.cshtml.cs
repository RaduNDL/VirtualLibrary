using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Pages.Products
{
    [Authorize(Roles = "Administrator")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        public IndexModel(AppDbContext context) => _context = context;

        public IList<Product> Products { get; set; } = [];

        [BindProperty(SupportsGet = true)]
        public string? q { get; set; }

        public async Task OnGetAsync()
        {
            var query = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .OrderByDescending(p => p.CreatedAtUtc)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(p =>
                    p.Title.Contains(term) ||
                    (p.Author != null && p.Author.Contains(term)) ||
                    (p.Isbn != null && p.Isbn.Contains(term)));
            }

            Products = await query.ToListAsync();
        }
    }
}
