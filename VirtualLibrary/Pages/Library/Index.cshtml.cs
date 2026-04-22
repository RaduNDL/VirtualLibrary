using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;
using VirtualLibrary.Services;

namespace VirtualLibrary.Pages.Library
{
    [AllowAnonymous]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ProductDiscoveryService _discovery;

        public IndexModel(AppDbContext context, ProductDiscoveryService discovery)
        {
            _context = context;
            _discovery = discovery;
        }

        public IList<Product> Products { get; set; } = new List<Product>();

        [BindProperty(SupportsGet = true)]
        public string? q { get; set; }

        public HashSet<int> FavoriteProductIds { get; set; } = new();

        public async Task OnGetAsync()
        {
            if (!string.IsNullOrWhiteSpace(q))
            {
                Products = await _discovery.SearchAsync(q, 100);
            }
            else
            {
                Products = await _context.Products
                    .AsNoTracking()
                    .Include(p => p.Category)
                    .Include(p => p.Supplier)
                    .OrderByDescending(p => p.CreatedAtUtc)
                    .ThenBy(p => p.Title)
                    .ToListAsync();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrWhiteSpace(userId))
            {
                FavoriteProductIds = await _context.Favorites
                    .AsNoTracking()
                    .Where(f => f.UserId == userId)
                    .Select(f => f.ProductId)
                    .ToHashSetAsync();
            }
        }

        public async Task<JsonResult> OnGetAutocompleteAsync(string term)
        {
            var suggestions = await _discovery.GetAutocompleteSuggestionsAsync(term);

            var data = suggestions.Select(s => new
            {
                id = s.Id,
                title = s.Title,
                url = $"/Products/Details/{s.Id}"
            });

            return new JsonResult(data);
        }
    }
}