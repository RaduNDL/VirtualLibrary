using System.Net.Http;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Services
{
    public class BookImporter
    {
        private readonly AppDbContext _db;
        private readonly IHttpClientFactory _http;
        private readonly IWebHostEnvironment _env;
        private readonly Random _rng = new();

        public BookImporter(AppDbContext db, IHttpClientFactory http, IWebHostEnvironment env)
        {
            _db = db; _http = http; _env = env;
        }

        public async Task<int> ImportGoogleBooksAsync(string subject = "fiction", int total = 60)
        {
            var client = _http.CreateClient();
            var imported = 0;

            var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Name == "Google Books")
                           ?? new Supplier { Name = "Google Books", ContactInfo = "https://books.google.com" };
            if (supplier.SupplierId == 0) _db.Suppliers.Add(supplier);

            var category = await _db.Categories.FirstOrDefaultAsync(c => c.Name == subject)
                           ?? new Category { Name = subject };
            if (category.CategoryId == 0) _db.Categories.Add(category);

            for (int start = 0; start < total; start += 40)
            {
                var take = Math.Min(40, total - start);
                var url = $"https://www.googleapis.com/books/v1/volumes?q=subject:{Uri.EscapeDataString(subject)}&maxResults={take}&startIndex={start}";
                using var resp = await client.GetAsync(url);
                if (!resp.IsSuccessStatusCode) break;

                using var stream = await resp.Content.ReadAsStreamAsync();
                var payload = await JsonSerializer.DeserializeAsync<GoogleBooksResponse>(stream, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (payload?.Items == null) continue;

                foreach (var item in payload.Items)
                {
                    var vi = item.VolumeInfo;
                    if (vi == null || string.IsNullOrWhiteSpace(vi.Title)) continue;

                    var isbn13 = vi.IndustryIdentifiers?
                        .FirstOrDefault(i => (i.Type ?? "").Contains("ISBN_13"))?.Identifier?
                        .Replace("-", "").Trim();

                    if (!string.IsNullOrWhiteSpace(isbn13) &&
                        await _db.Products.AnyAsync(p => p.Isbn == isbn13))
                        continue;

                    string? localCover = null;
                    var rawThumb = vi.ImageLinks?.Thumbnail ?? vi.ImageLinks?.SmallThumbnail;
                    if (!string.IsNullOrWhiteSpace(rawThumb))
                    {
                        var coverUrl = rawThumb.Replace("http://", "https://");
                        try
                        {
                            var bytes = await client.GetByteArrayAsync(coverUrl);
                            var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", "books");
                            Directory.CreateDirectory(uploadsRoot);
                            var fileName = $"{Guid.NewGuid():N}.jpg";
                            var fullPath = Path.Combine(uploadsRoot, fileName);
                            await File.WriteAllBytesAsync(fullPath, bytes);
                            localCover = Path.Combine("uploads", "books", fileName).Replace('\\', '/');
                        }
                        catch {  }
                    }

                    var product = new Product
                    {
                        Title = TrimLen(vi.Title, 200),
                        Author = vi.Authors != null ? TrimLen(string.Join(", ", vi.Authors), 200) : null,
                        Description = TrimLen(vi.Description, 4000),
                        Isbn = string.IsNullOrWhiteSpace(isbn13) ? null : TrimLen(isbn13, 13),
                        Price = (decimal)(_rng.Next(25, 120) + _rng.NextDouble()), 
                        Stock = _rng.Next(0, 50),
                        ImagePath = localCover,
                        Category = category,
                        Supplier = supplier,
                        CreatedAtUtc = DateTime.UtcNow
                    };

                    _db.Products.Add(product);
                    imported++;
                }
            }

            await _db.SaveChangesAsync();
            return imported;
        }

        private static string TrimLen(string? s, int max) =>
            string.IsNullOrEmpty(s) ? s! : (s.Length <= max ? s : s[..max]);

        private class GoogleBooksResponse { public List<GoogleBookItem>? Items { get; set; } }
        private class GoogleBookItem { public VolumeInfo? VolumeInfo { get; set; } }
        private class VolumeInfo
        {
            public string? Title { get; set; }
            public List<string>? Authors { get; set; }
            public string? Description { get; set; }
            public List<IndustryIdentifier>? IndustryIdentifiers { get; set; }
            public ImageLinks? ImageLinks { get; set; }
        }
        private class IndustryIdentifier { public string? Type { get; set; } public string? Identifier { get; set; } }
        private class ImageLinks { public string? SmallThumbnail { get; set; } public string? Thumbnail { get; set; } }
    }
}
