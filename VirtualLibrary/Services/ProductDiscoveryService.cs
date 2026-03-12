using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Services
{
    public class ProductDiscoveryService
    {
        private const string CacheKey = "product_discovery_index";
        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;

        public ProductDiscoveryService(AppDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<List<Product>> SearchAsync(string query, int take = 100)
        {
            var index = await GetIndexAsync();

            if (string.IsNullOrWhiteSpace(query))
            {
                return index.Documents
                    .Select(d => d.Product)
                    .Take(take)
                    .ToList();
            }

            var queryTokens = Tokenize(query);
            var queryVector = BuildVector(queryTokens, index.Idf);
            var queryNorm = Norm(queryVector);
            var normalizedQuery = Normalize(query);

            return index.Documents
                .Select(d => new
                {
                    d.Product,
                    Score = Cosine(queryVector, queryNorm, d.Vector, d.VectorNorm) +
                            SearchTitleBoost(d.NormalizedTitle, normalizedQuery)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Product.Title)
                .Take(take)
                .Select(x => x.Product)
                .ToList();
        }

        public async Task<List<AutocompleteSuggestionDto>> GetAutocompleteSuggestionsAsync(string term, int take = 8)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return new List<AutocompleteSuggestionDto>();
            }

            var index = await GetIndexAsync();
            var normalizedTerm = Normalize(term);

            if (string.IsNullOrWhiteSpace(normalizedTerm))
            {
                return new List<AutocompleteSuggestionDto>();
            }

            return index.Documents
                .Select(d => new
                {
                    d.Product.Id,
                    d.Product.Title,
                    Score = AutocompleteScore(d.NormalizedTitle, normalizedTerm)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Title)
                .Take(take)
                .Select(x => new AutocompleteSuggestionDto
                {
                    ProductId = x.Id,
                    Title = x.Title
                })
                .ToList();
        }

        public async Task<List<SimilarProductResult>> GetSimilarProductsAsync(int productId, int take = 6)
        {
            var index = await GetIndexAsync();
            var current = index.Documents.FirstOrDefault(x => x.Product.Id == productId);

            if (current == null)
            {
                return new List<SimilarProductResult>();
            }

            return index.Documents
                .Where(x => x.Product.Id != productId)
                .Select(x => new SimilarProductResult
                {
                    Product = x.Product,
                    Similarity = Cosine(current.Vector, current.VectorNorm, x.Vector, x.VectorNorm)
                })
                .Where(x => x.Similarity > 0.05)
                .OrderByDescending(x => x.Similarity)
                .Take(take)
                .ToList();
        }

        public void InvalidateCache()
        {
            _cache.Remove(CacheKey);
        }

        private async Task<SearchIndex> GetIndexAsync()
        {
            if (_cache.TryGetValue(CacheKey, out SearchIndex? cached) && cached != null)
            {
                return cached;
            }

            var products = await _db.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .ToListAsync();

            var docs = products.Select(p =>
            {
                var fullText = $"{p.Title} {p.Description} {p.Author} {p.Category?.Name} {p.Publisher}";
                var tokens = Tokenize(fullText);

                return new ProductDocument
                {
                    Product = p,
                    Tokens = tokens,
                    NormalizedTitle = Normalize(p.Title)
                };
            }).ToList();

            var df = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var doc in docs)
            {
                foreach (var token in doc.Tokens.Distinct())
                {
                    df[token] = df.TryGetValue(token, out var count) ? count + 1 : 1;
                }
            }

            var documentCount = Math.Max(docs.Count, 1);

            var idf = df.ToDictionary(
                x => x.Key,
                x => Math.Log((double)(documentCount + 1) / (x.Value + 1)) + 1.0,
                StringComparer.Ordinal);

            foreach (var doc in docs)
            {
                doc.Vector = BuildVector(doc.Tokens, idf);
                doc.VectorNorm = Norm(doc.Vector);
            }

            var index = new SearchIndex
            {
                Documents = docs,
                Idf = idf
            };

            _cache.Set(CacheKey, index, TimeSpan.FromMinutes(15));
            return index;
        }

        private static Dictionary<string, double> BuildVector(List<string> tokens, Dictionary<string, double> idf)
        {
            var vector = new Dictionary<string, double>(StringComparer.Ordinal);

            if (tokens.Count == 0)
            {
                return vector;
            }

            var tf = tokens
                .GroupBy(x => x)
                .ToDictionary(g => g.Key, g => (double)g.Count() / tokens.Count, StringComparer.Ordinal);

            foreach (var item in tf)
            {
                if (idf.TryGetValue(item.Key, out var idfValue))
                {
                    vector[item.Key] = item.Value * idfValue;
                }
            }

            return vector;
        }

        private static double Cosine(
            Dictionary<string, double> a,
            double normA,
            Dictionary<string, double> b,
            double normB)
        {
            if (normA == 0 || normB == 0)
            {
                return 0;
            }

            double dot = 0;
            var smaller = a.Count <= b.Count ? a : b;
            var larger = ReferenceEquals(smaller, a) ? b : a;

            foreach (var kv in smaller)
            {
                if (larger.TryGetValue(kv.Key, out var other))
                {
                    dot += kv.Value * other;
                }
            }

            return dot / (normA * normB);
        }

        private static double Norm(Dictionary<string, double> vector)
        {
            return Math.Sqrt(vector.Values.Sum(x => x * x));
        }

        private static double SearchTitleBoost(string normalizedTitle, string normalizedQuery)
        {
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return 0;
            }

            double score = 0;

            if (normalizedTitle == normalizedQuery)
            {
                score += 1.0;
            }

            if (normalizedTitle.Contains(normalizedQuery, StringComparison.Ordinal))
            {
                score += 0.4;
            }

            return score;
        }

        private static double AutocompleteScore(string normalizedTitle, string normalizedTerm)
        {
            if (string.IsNullOrWhiteSpace(normalizedTerm) || string.IsNullOrWhiteSpace(normalizedTitle))
            {
                return 0;
            }

            var maxLen = Math.Min(normalizedTerm.Length, normalizedTitle.Length);
            var titleFragment = normalizedTitle[..maxLen];
            var distance = Levenshtein(titleFragment, normalizedTerm);

            if (distance > 3)
            {
                return 0;
            }

            return distance switch
            {
                0 => 100,
                1 => 70,
                2 => 40,
                3 => 20,
                _ => 0
            };
        }

        private static List<string> Tokenize(string? text)
        {
            var normalized = Normalize(text);

            return string.IsNullOrWhiteSpace(normalized)
                ? new List<string>()
                : normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private static string Normalize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var normalized = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);

                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
            }

            return string.Join(" ", sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private static int Levenshtein(string a, string b)
        {
            if (string.IsNullOrEmpty(a))
            {
                return b.Length;
            }

            if (string.IsNullOrEmpty(b))
            {
                return a.Length;
            }

            var dp = new int[a.Length + 1, b.Length + 1];

            for (var i = 0; i <= a.Length; i++)
            {
                dp[i, 0] = i;
            }

            for (var j = 0; j <= b.Length; j++)
            {
                dp[0, j] = j;
            }

            for (var i = 1; i <= a.Length; i++)
            {
                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;

                    dp[i, j] = Math.Min(
                        Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + cost);
                }
            }

            return dp[a.Length, b.Length];
        }

        private class SearchIndex
        {
            public List<ProductDocument> Documents { get; set; } = new();
            public Dictionary<string, double> Idf { get; set; } = new(StringComparer.Ordinal);
        }

        private class ProductDocument
        {
            public Product Product { get; set; } = null!;
            public string NormalizedTitle { get; set; } = string.Empty;
            public List<string> Tokens { get; set; } = new();
            public Dictionary<string, double> Vector { get; set; } = new(StringComparer.Ordinal);
            public double VectorNorm { get; set; }
        }
    }

    public class AutocompleteSuggestionDto
    {
        public int ProductId { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    public class SimilarProductResult
    {
        public Product Product { get; set; } = null!;
        public double Similarity { get; set; }

        public int SimilarityPercent => (int)Math.Round(Math.Max(0, Math.Min(1, Similarity)) * 100);
    }
}
