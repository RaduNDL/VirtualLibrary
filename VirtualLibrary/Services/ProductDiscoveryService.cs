using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Services
{
    public sealed class ProductDiscoveryService
    {
        private const string CacheKey = "product_discovery_index_v5";

        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;

        private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
        {
            "a", "an", "the",
            "at", "by", "for", "from", "in", "into", "of", "off",
            "on", "onto", "out", "over", "per", "to", "up", "via",
            "with", "within", "without",
            "about", "above", "across", "after", "against", "along",
            "among", "around", "as", "before", "behind", "below",
            "beneath", "beside", "between", "beyond", "during",
            "except", "inside", "near", "outside", "since",
            "through", "throughout", "under", "underneath", "until",
            "upon", "versus", "vs",
            "and", "but", "or", "nor", "so", "yet",
        };

        public ProductDiscoveryService(AppDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<List<Product>> SearchAsync(string query, int take = 100)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<Product>();

            string normalized = Normalize(query);
            if (string.IsNullOrWhiteSpace(normalized))
                return new List<Product>();

            string[] queryWords = RemoveStopWords(SplitWords(normalized), keepLast: true);

            if (queryWords.Length == 0)
                return new List<Product>();

            var index = await GetIndexAsync();

            return index.Documents
                .Select(d => new
                {
                    d.Product,
                    Match = ComputeMatch(queryWords, d.TitleWords)
                })
                .Where(x => x.Match.IsMatch)
                .OrderBy(x => x.Match.TotalDistance)
                .ThenBy(x => x.Product.Title.Length)
                .ThenBy(x => x.Product.Title)
                .Take(take)
                .Select(x => x.Product)
                .ToList();
        }

        public async Task<List<ProductSuggestionDto>> GetAutocompleteSuggestionsAsync(string term, int take = 8)
        {
            if (string.IsNullOrWhiteSpace(term))
                return new List<ProductSuggestionDto>();

            string normalized = Normalize(term);
            if (string.IsNullOrWhiteSpace(normalized))
                return new List<ProductSuggestionDto>();

            string[] queryWords = RemoveStopWords(SplitWords(normalized), keepLast: true);

            if (queryWords.Length == 0)
                return new List<ProductSuggestionDto>();

            var index = await GetIndexAsync();

            return index.Documents
                .Select(d => new
                {
                    d.Product,
                    Match = ComputeMatch(queryWords, d.TitleWords)
                })
                .Where(x => x.Match.IsMatch)
                .OrderBy(x => x.Match.TotalDistance)
                .ThenBy(x => x.Product.Title.Length)
                .ThenBy(x => x.Product.Title)
                .Take(take)
                .Select(x => new ProductSuggestionDto
                {
                    Id = x.Product.Id,
                    Title = x.Product.Title,
                    Score = x.Match.TotalDistance
                })
                .ToList();
        }

        private static string[] RemoveStopWords(string[] words, bool keepLast)
        {
            if (words.Length == 0)
                return words;

            var result = new List<string>(words.Length);

            for (int i = 0; i < words.Length; i++)
            {
                bool isLast = i == words.Length - 1;

                if (isLast && keepLast)
                {
                    result.Add(words[i]);
                    continue;
                }

                if (!StopWords.Contains(words[i]))
                    result.Add(words[i]);
            }

            return result.ToArray();
        }

        private static MatchResult ComputeMatch(string[] queryWords, string[] titleWords)
        {
            if (queryWords.Length == 0 || titleWords.Length == 0)
                return MatchResult.NoMatch();

            if (queryWords.Length > titleWords.Length)
                return MatchResult.NoMatch();

            int lastIndex = queryWords.Length - 1;
            bool[] used = new bool[titleWords.Length];
            int totalDistance = 0;

            for (int qi = 0; qi < lastIndex; qi++)
            {
                string qWord = queryWords[qi];
                int maxDist = GetMaxDistance(qWord.Length, false);

                int bestDist = int.MaxValue;
                int bestTi = -1;

                for (int ti = 0; ti < titleWords.Length; ti++)
                {
                    if (used[ti]) continue;

                    int dist = ComputeLevenshteinDistance(qWord, titleWords[ti]);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestTi = ti;
                    }
                }

                if (bestTi == -1 || bestDist > maxDist)
                    return MatchResult.NoMatch();

                used[bestTi] = true;
                totalDistance += bestDist;
            }

            {
                string prefix = queryWords[lastIndex];
                int maxDist = GetMaxDistance(prefix.Length, true);

                int bestDist = int.MaxValue;
                int bestTi = -1;

                for (int ti = 0; ti < titleWords.Length; ti++)
                {
                    if (used[ti]) continue;

                    string titleWord = titleWords[ti];
                    string comparable = titleWord.Length >= prefix.Length
                        ? titleWord.Substring(0, prefix.Length)
                        : titleWord;

                    int dist = ComputeLevenshteinDistance(prefix, comparable);

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestTi = ti;
                    }
                }

                if (bestTi == -1 || bestDist > maxDist)
                    return MatchResult.NoMatch();

                totalDistance += bestDist;
            }

            return MatchResult.Yes(totalDistance);
        }

        private static int ComputeLevenshteinDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
            if (string.IsNullOrEmpty(b)) return a.Length;

            int n = a.Length;
            int m = b.Length;

            var prev = new int[m + 1];
            var curr = new int[m + 1];

            for (int j = 0; j <= m; j++)
                prev[j] = j;

            for (int i = 1; i <= n; i++)
            {
                curr[0] = i;
                for (int j = 1; j <= m; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(Math.Min(prev[j] + 1, curr[j - 1] + 1), prev[j - 1] + cost);
                }
                (prev, curr) = (curr, prev);
            }

            return prev[m];
        }

        private static int GetMaxDistance(int tokenLength, bool isPrefix)
        {
            if (tokenLength <= 2) return 0;
            if (tokenLength <= 4) return isPrefix ? 1 : 0;
            if (tokenLength <= 7) return 1;
            return 2;
        }

        private async Task<SearchIndex> GetIndexAsync()
        {
            if (_cache.TryGetValue(CacheKey, out SearchIndex? cached) && cached is not null)
                return cached;

            var products = await _db.Products
                .AsNoTracking()
                .Where(p => !string.IsNullOrWhiteSpace(p.Title))
                .ToListAsync();

            var index = new SearchIndex
            {
                Documents = products.Select(BuildDocument).ToList()
            };

            _cache.Set(CacheKey, index, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
            });

            return index;
        }

        private static IndexedProductDocument BuildDocument(Product product)
        {
            string[] allWords = SplitWords(Normalize(product.Title));
            string[] filteredWords = RemoveStopWords(allWords, false);

            return new IndexedProductDocument
            {
                Product = product,
                TitleWords = filteredWords.Length > 0 ? filteredWords : allWords
            };
        }

        private static string[] SplitWords(string text) =>
            text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        private static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string formD = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);

            foreach (char c in formD)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;

                sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
            }

            return string.Join(" ", sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private sealed class SearchIndex
        {
            public List<IndexedProductDocument> Documents { get; set; } = new();
        }

        private sealed class IndexedProductDocument
        {
            public Product Product { get; set; } = null!;
            public string[] TitleWords { get; set; } = Array.Empty<string>();
        }

        private readonly struct MatchResult
        {
            public bool IsMatch { get; }
            public int TotalDistance { get; }

            private MatchResult(bool isMatch, int totalDistance)
            {
                IsMatch = isMatch;
                TotalDistance = totalDistance;
            }

            public static MatchResult NoMatch() => new(false, int.MaxValue);
            public static MatchResult Yes(int totalDistance) => new(true, totalDistance);
        }
    }

    public sealed class ProductSuggestionDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Score { get; set; }
    }
}