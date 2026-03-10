using System;
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
            var allProducts = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(q))
            {
                Products = PerformTfIdfSearch(allProducts, q);
            }
            else
            {
                Products = allProducts;
            }

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

        private List<Product> PerformTfIdfSearch(List<Product> allProducts, string searchQuery)
        {
            var separators = new[] { ' ', '\t', '\n', '\r', '.', ',', ';', '!', '?', '-', ':' };
            var queryTerms = searchQuery.ToLowerInvariant().Split(separators, StringSplitOptions.RemoveEmptyEntries);

            if (queryTerms.Length == 0) return allProducts;

            var documents = allProducts.Select(p => p.Title.ToLowerInvariant()).ToList();
            var N = documents.Count;
            var idfMap = new Dictionary<string, double>();

            foreach (var term in queryTerms.Distinct())
            {
                var df = documents.Count(d => d.Split(separators, StringSplitOptions.RemoveEmptyEntries).Contains(term));
                idfMap[term] = Math.Log((double)N / (1 + df)) + 1;
            }

            return allProducts.Select(p =>
            {
                var docTerms = p.Title.ToLowerInvariant().Split(separators, StringSplitOptions.RemoveEmptyEntries);
                var docLength = docTerms.Length;
                double score = 0;

                if (docLength > 0)
                {
                    foreach (var term in queryTerms)
                    {
                        var tf = (double)docTerms.Count(t => t == term) / docLength;
                        if (idfMap.TryGetValue(term, out var idf))
                        {
                            score += tf * idf;
                        }
                    }
                }
                return new { Product = p, Score = score };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Product)
            .ToList();
        }
    }
}