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
        private readonly ILogger<BookImporter> _logger;
        private readonly Random _rng = new();

        public BookImporter(
            AppDbContext db,
            IHttpClientFactory http,
            IWebHostEnvironment env,
            ILogger<BookImporter> logger)
        {
            _db = db;
            _http = http;
            _env = env;
            _logger = logger;
        }

        public async Task<int> ImportGoogleBooksAsync(string subject = "fiction", int total = 60)
        {
            var client = _http.CreateClient("PdfClient");
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("VirtualLibrary/1.0 (Student Project; contact@example.com)");

            var imported = 0;

            _logger.LogInformation("=== IMPORT START: subject='{Subject}', count={Count} ===", subject, total);

            try
            {
                var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Name == "Open Library");
                if (supplier == null)
                {
                    supplier = new Supplier
                    {
                        Name = "Open Library",
                        ContactInfo = "https://openlibrary.org"
                    };
                    _db.Suppliers.Add(supplier);
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("Created supplier: Open Library (ID={Id})", supplier.SupplierId);
                }

                var catName = subject.Length > 100 ? subject[..100] : subject;
                var category = await _db.Categories.FirstOrDefaultAsync(c => c.Name == catName);
                if (category == null)
                {
                    category = new Category { Name = catName };
                    _db.Categories.Add(category);
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("Created category: {Name} (ID={Id})", category.Name, category.CategoryId);
                }

                for (int offset = 0; offset < total; offset += 50)
                {
                    var take = Math.Min(50, total - offset);
                    var url = $"https://openlibrary.org/search.json?subject={Uri.EscapeDataString(subject)}&limit={take}&offset={offset}&fields=title,author_name,isbn,first_sentence,cover_i,ia,key";

                    _logger.LogInformation("Fetching Open Library: {Url}", url);

                    HttpResponseMessage resp;
                    try
                    {
                        resp = await client.GetAsync(url);
                    }
                    catch (TaskCanceledException)
                    {
                        _logger.LogWarning("Request timed out for offset={Offset}", offset);
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "HTTP request failed");
                        break;
                    }

                    _logger.LogInformation("Response: {StatusCode}", resp.StatusCode);

                    if (!resp.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Open Library error: {Status}", resp.StatusCode);
                        break;
                    }

                    var json = await resp.Content.ReadAsStringAsync();

                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("docs", out var docs) || docs.GetArrayLength() == 0)
                    {
                        _logger.LogWarning("No docs returned at offset={Offset}", offset);
                        break;
                    }

                    _logger.LogInformation("Got {Count} books from Open Library", docs.GetArrayLength());

                    foreach (var book in docs.EnumerateArray())
                    {
                        try
                        {
                           
                            var title = book.TryGetProperty("title", out var tProp) ? tProp.GetString() : null;
                            if (string.IsNullOrWhiteSpace(title)) continue;

                            string? author = null;
                            if (book.TryGetProperty("author_name", out var authorsArr) && authorsArr.GetArrayLength() > 0)
                            {
                                var names = new List<string>();
                                foreach (var a in authorsArr.EnumerateArray())
                                {
                                    var n = a.GetString();
                                    if (!string.IsNullOrEmpty(n)) names.Add(n);
                                }
                                author = string.Join(", ", names);
                            }

                            string? isbn = null;
                            if (book.TryGetProperty("isbn", out var isbnArr) && isbnArr.GetArrayLength() > 0)
                            {
                                foreach (var i in isbnArr.EnumerateArray())
                                {
                                    var val = i.GetString()?.Replace("-", "").Trim();
                                    if (val != null && val.Length == 13) { isbn = val; break; }
                                }
                                if (isbn == null)
                                {
                                    isbn = isbnArr[0].GetString()?.Replace("-", "").Trim();
                                    if (isbn != null && isbn.Length > 13) isbn = isbn[..13];
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(isbn) &&
                                await _db.Products.AnyAsync(p => p.Isbn == isbn))
                            {
                                _logger.LogDebug("Skipping duplicate: {Isbn}", isbn);
                                continue;
                            }

                            string? description = null;
                            if (book.TryGetProperty("first_sentence", out var sentences) && sentences.GetArrayLength() > 0)
                            {
                                description = sentences[0].GetString();
                            }

                            string? localCover = null;
                            if (book.TryGetProperty("cover_i", out var coverIdProp))
                            {
                                var coverId = coverIdProp.GetInt32();
                                var coverUrl = $"https://covers.openlibrary.org/b/id/{coverId}-M.jpg";
                                try
                                {
                                    var bytes = await client.GetByteArrayAsync(coverUrl);
                                    if (bytes.Length > 500) 
                                    {
                                        var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", "books");
                                        Directory.CreateDirectory(uploadsRoot);
                                        var fileName = $"{Guid.NewGuid():N}.jpg";
                                        await File.WriteAllBytesAsync(Path.Combine(uploadsRoot, fileName), bytes);
                                        localCover = $"uploads/books/{fileName}";
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogDebug(ex, "Cover download failed for {Title}", title);
                                }
                            }

                            string? pdfSource = null;
                            if (book.TryGetProperty("ia", out var iaArr) && iaArr.GetArrayLength() > 0)
                            {
                                pdfSource = "Pending"; 
                            }

                            var product = new Product
                            {
                                Title = Trim(title, 200),
                                Author = author != null ? Trim(author, 200) : null,
                                Description = description != null ? Trim(description, 4000) : null,
                                Isbn = isbn,
                                Price = Math.Round((decimal)(_rng.Next(15, 100) + _rng.NextDouble()), 2),
                                Stock = _rng.Next(1, 50),
                                ImagePath = localCover,
                                PdfSource = pdfSource,
                                CategoryId = category.CategoryId,
                                SupplierId = supplier.SupplierId,
                                CreatedAtUtc = DateTime.UtcNow
                            };

                            _db.Products.Add(product);
                            imported++;
                            _logger.LogInformation("  ✓ '{Title}' by {Author}", product.Title, product.Author ?? "Unknown");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error processing book");
                        }
                    }

                    if (imported > 0)
                    {
                        try
                        {
                            var saved = await _db.SaveChangesAsync();
                            _logger.LogInformation("💾 Saved {Count} records to database", saved);
                        }
                        catch (DbUpdateException dbEx)
                        {
                            _logger.LogError(dbEx, "❌ DATABASE SAVE FAILED: {Inner}", dbEx.InnerException?.Message);
                            foreach (var entry in _db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
                                entry.State = EntityState.Detached;
                        }
                    }

                    await Task.Delay(1000);
                }

                _logger.LogInformation("=== IMPORT COMPLETE: {Count} books imported ===", imported);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== IMPORT CRASHED ===");
            }

            return imported;
        }

        private static string Trim(string? s, int max) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max]);
    }
}