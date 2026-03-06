using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Pages.Library
{
    [AllowAnonymous]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<Product> Products { get; set; } = new List<Product>();

        [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)]
        public string? q { get; set; }

        public HashSet<int> FavoriteProductIds { get; set; } = new HashSet<int>();

        public async Task OnGetAsync()
        {
            var query = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(p =>
                    p.Title.Contains(q) ||
                    (p.Author != null && p.Author.Contains(q)) ||
                    (p.Isbn != null && p.Isbn.Contains(q)));
            }

            Products = await query.ToListAsync();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(userId))
            {
                var favIds = await _context.Favorites
                    .Where(f => f.UserId == userId)
                    .Select(f => f.ProductId)
                    .ToListAsync();

                FavoriteProductIds = favIds.ToHashSet();
            }
        }
    }
}