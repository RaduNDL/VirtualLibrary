using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Pages.Library
{
    [AllowAnonymous]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        public IndexModel(AppDbContext context) => _context = context;

        public IList<Product> Products { get; private set; } = new List<Product>();

        [BindProperty(SupportsGet = true)]
        public string? q { get; set; }

        public async Task OnGetAsync()
        {
            var qry = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var pattern = $"%{q.Trim()}%";
                qry = qry.Where(p =>
                    EF.Functions.Like(p.Title, pattern) ||
                    (p.Author != null && EF.Functions.Like(p.Author, pattern)) ||
                    (p.Isbn != null && EF.Functions.Like(p.Isbn, pattern))
                );
            }

            Products = await qry
                .OrderBy(p => p.Title)
                .ToListAsync();
        }
    }
}
