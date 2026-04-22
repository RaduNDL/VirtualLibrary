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
        private readonly BookPdfGenerator _bookPdfGenerator;
        private readonly LuceneSpecificationSearchService _luceneSpecificationSearchService;
        private readonly Random _rng = new();

        public BookImporter(
            AppDbContext db,
            IHttpClientFactory http,
            IWebHostEnvironment env,
            ILogger<BookImporter> logger,
            BookPdfGenerator bookPdfGenerator,
            LuceneSpecificationSearchService luceneSpecificationSearchService)
        {
            _db = db;
            _http = http;
            _env = env;
            _logger = logger;
            _bookPdfGenerator = bookPdfGenerator;
            _luceneSpecificationSearchService = luceneSpecificationSearchService;
        }

        public async Task<int> ImportGoogleBooksAsync(string subject = "fiction", int total = 60)
        {
            var client = _http.CreateClient("PdfClient");
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("VirtualLibrary/1.0 (Student Project)");

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
                }

                var catName = subject.Length > 100 ? subject[..100] : subject;
                var category = await _db.Categories.FirstOrDefaultAsync(c => c.Name == catName);
                if (category == null)
                {
                    category = new Category { Name = catName };
                    _db.Categories.Add(category);
                    await _db.SaveChangesAsync();
                }

                for (int offset = 0; offset < total; offset += 50)
                {
                    var take = Math.Min(50, total - offset);

                    var url =
                        $"https://openlibrary.org/search.json?subject={Uri.EscapeDataString(subject)}" +
                        $"&limit={take}&offset={offset}" +
                        $"&fields=title,author_name,isbn,first_sentence,cover_i,language,first_publish_year,publisher,number_of_pages_median";

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
                        _logger.LogError(ex, "HTTP request failed while calling Open Library");
                        break;
                    }

                    if (!resp.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Open Library returned error status: {Status}", resp.StatusCode);
                        break;
                    }

                    var json = await resp.Content.ReadAsStringAsync();

                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("docs", out var docs) || docs.ValueKind != JsonValueKind.Array || docs.GetArrayLength() == 0)
                    {
                        _logger.LogWarning("No docs returned at offset={Offset}", offset);
                        break;
                    }

                    foreach (var book in docs.EnumerateArray())
                    {
                        try
                        {
                            var title = GetString(book, "title");
                            if (string.IsNullOrWhiteSpace(title))
                                continue;

                            var author = GetFirstArrayJoined(book, "author_name");
                            var isbn = ExtractBestIsbn(book);

                            if (!string.IsNullOrWhiteSpace(isbn))
                            {
                                var existsByIsbn = await _db.Products.AnyAsync(p => p.Isbn == isbn);
                                if (existsByIsbn)
                                    continue;
                            }
                            else
                            {
                                var existsByTitleAuthor = await _db.Products.AnyAsync(p =>
                                    p.Title == title && p.Author == author);

                                if (existsByTitleAuthor)
                                    continue;
                            }

                            var description = ExtractFirstSentence(book);
                            var localCover = await DownloadCoverAsync(client, book, title);

                            int? publishedYear = TryGetInt(book, "first_publish_year");
                            var publisher = GetFirstArrayValue(book, "publisher");
                            int? pageCount = TryGetInt(book, "number_of_pages_median");
                            var language = GetFirstArrayValue(book, "language");

                            var extra = await EnrichFromGoogleBooksAsync(client, title!, author, isbn);

                            publisher = FirstNotEmpty(extra.Publisher, publisher);
                            language = FirstNotEmpty(extra.Language, language);
                            pageCount ??= extra.PageCount;
                            publishedYear ??= extra.PublishedYear;

                            description = FirstNotEmpty(extra.Description, description);

                            description = BuildGeneratedDescription(
                                title!,
                                author,
                                subject,
                                publisher,
                                publishedYear,
                                pageCount,
                                language,
                                extra.Rating,
                                description);

                            var product = new Product
                            {
                                Title = TrimOrEmpty(title, 200),
                                Author = TrimOrNull(author, 200),
                                Description = TrimOrNull(description, 4000),
                                Isbn = TrimIsbnOrNull(isbn),
                                Price = Math.Round((decimal)(_rng.Next(15, 100) + _rng.NextDouble()), 2),
                                ImagePath = TrimOrNull(localCover, 500),
                                CategoryId = category.CategoryId,
                                SupplierId = supplier.SupplierId,
                                CreatedAtUtc = DateTime.UtcNow,
                                UpdatedAtUtc = DateTime.UtcNow,
                                Publisher = TrimOrNull(publisher, 200),
                                PublishedYear = publishedYear,
                                PageCount = pageCount,
                                Language = TrimOrNull(language, 50),
                                Rating = NormalizeRating(extra.Rating)
                            };

                            _db.Products.Add(product);
                            await _db.SaveChangesAsync();

                            product.Category = category;
                            product.Supplier = supplier;

                            var descriptionPdfPath = await _bookPdfGenerator.GenerateBookDescriptionPdfAsync(product);

                            if (!string.IsNullOrWhiteSpace(descriptionPdfPath))
                            {
                                product.DescriptionPdfPath = descriptionPdfPath;
                                product.UpdatedAtUtc = DateTime.UtcNow;
                                await _db.SaveChangesAsync();

                                await _luceneSpecificationSearchService.IndexProductAsync(product.Id);
                            }

                            imported++;

                            _logger.LogInformation(
                                "Imported '{Title}' by {Author}. PDF generated and indexed.",
                                product.Title,
                                product.Author ?? "Unknown");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error processing a book record");
                        }
                    }

                    await Task.Delay(800);
                }

                _logger.LogInformation("=== IMPORT COMPLETE: {Count} books imported ===", imported);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== IMPORT CRASHED ===");
            }

            return imported;
        }

        private async Task<string?> DownloadCoverAsync(HttpClient client, JsonElement book, string title)
        {
            try
            {
                if (!book.TryGetProperty("cover_i", out var coverIdProp) || coverIdProp.ValueKind != JsonValueKind.Number)
                    return null;

                var coverId = coverIdProp.GetInt32();
                var coverUrl = $"https://covers.openlibrary.org/b/id/{coverId}-L.jpg";
                var bytes = await client.GetByteArrayAsync(coverUrl);

                if (bytes.Length < 500)
                    return null;

                var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", "books");
                Directory.CreateDirectory(uploadsRoot);

                var fileName = $"{Guid.NewGuid():N}.jpg";
                var fullPath = Path.Combine(uploadsRoot, fileName);

                await File.WriteAllBytesAsync(fullPath, bytes);

                return $"uploads/books/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Cover download failed for {Title}", title);
                return null;
            }
        }

        private async Task<GoogleBooksExtra> EnrichFromGoogleBooksAsync(HttpClient client, string title, string? author, string? isbn)
        {
            try
            {
                string query;

                if (!string.IsNullOrWhiteSpace(isbn))
                    query = $"isbn:{isbn}";
                else if (!string.IsNullOrWhiteSpace(author))
                    query = $"intitle:{title} inauthor:{author}";
                else
                    query = $"intitle:{title}";

                var url = $"https://www.googleapis.com/books/v1/volumes?q={Uri.EscapeDataString(query)}&maxResults=1";
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return new GoogleBooksExtra();

                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("items", out var items) ||
                    items.ValueKind != JsonValueKind.Array ||
                    items.GetArrayLength() == 0)
                {
                    return new GoogleBooksExtra();
                }

                var volumeInfo = items[0].GetProperty("volumeInfo");

                return new GoogleBooksExtra
                {
                    Description = TryGetString(volumeInfo, "description"),
                    Publisher = TryGetString(volumeInfo, "publisher"),
                    Language = TryGetString(volumeInfo, "language"),
                    PageCount = TryGetInt(volumeInfo, "pageCount"),
                    Rating = TryGetDoubleAsDecimal(volumeInfo, "averageRating"),
                    PublishedYear = ExtractYear(TryGetString(volumeInfo, "publishedDate"))
                };
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Google Books enrichment failed for {Title}", title);
                return new GoogleBooksExtra();
            }
        }

        private static string BuildGeneratedDescription(
            string title,
            string? author,
            string? subject,
            string? publisher,
            int? publishedYear,
            int? pageCount,
            string? language,
            decimal? rating,
            string? existingDescription)
        {
            if (!string.IsNullOrWhiteSpace(existingDescription) &&
                !existingDescription.Trim().Equals("No description available.", StringComparison.OrdinalIgnoreCase))
            {
                return existingDescription.Trim();
            }

            var parts = new List<string>
            {
                $"\"{title}\" is a catalogued book entry available in the VirtualLibrary collection."
            };

            if (!string.IsNullOrWhiteSpace(author))
                parts.Add($"The listed author for this title is {author}.");

            if (!string.IsNullOrWhiteSpace(subject))
                parts.Add($"It is currently grouped under the {subject} category.");

            if (!string.IsNullOrWhiteSpace(publisher) && publishedYear.HasValue)
                parts.Add($"The available edition metadata indicates publication by {publisher} in {publishedYear.Value}.");
            else if (!string.IsNullOrWhiteSpace(publisher))
                parts.Add($"The available edition metadata indicates publication by {publisher}.");
            else if (publishedYear.HasValue)
                parts.Add($"The recorded publication year for this entry is {publishedYear.Value}.");

            if (pageCount.HasValue)
                parts.Add($"This edition contains approximately {pageCount.Value} pages.");

            if (!string.IsNullOrWhiteSpace(language))
                parts.Add($"The language recorded for this book is {language}.");

            if (rating.HasValue)
                parts.Add($"The imported metadata also includes an average reader rating of {rating.Value:0.0} out of 5.");

            parts.Add("This description was automatically generated from bibliographic metadata in order to provide a complete and natural-looking product presentation inside the application.");

            return string.Join(" ", parts);
        }

        private static string? ExtractFirstSentence(JsonElement book)
        {
            if (!book.TryGetProperty("first_sentence", out var firstSentenceProp))
                return null;

            if (firstSentenceProp.ValueKind == JsonValueKind.Array && firstSentenceProp.GetArrayLength() > 0)
                return firstSentenceProp[0].GetString();

            if (firstSentenceProp.ValueKind == JsonValueKind.String)
                return firstSentenceProp.GetString();

            return null;
        }

        private static string? ExtractBestIsbn(JsonElement book)
        {
            if (!book.TryGetProperty("isbn", out var isbnArr) ||
                isbnArr.ValueKind != JsonValueKind.Array ||
                isbnArr.GetArrayLength() == 0)
            {
                return null;
            }

            foreach (var i in isbnArr.EnumerateArray())
            {
                var raw = i.GetString()?.Replace("-", "").Trim();
                if (!string.IsNullOrWhiteSpace(raw) && raw.Length == 13)
                    return raw;
            }

            foreach (var i in isbnArr.EnumerateArray())
            {
                var raw = i.GetString()?.Replace("-", "").Trim();
                if (!string.IsNullOrWhiteSpace(raw))
                    return raw.Length > 13 ? raw[..13] : raw;
            }

            return null;
        }

        private static string? GetString(JsonElement parent, string propertyName)
        {
            if (parent.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();

            return null;
        }

        private static string? GetFirstArrayValue(JsonElement parent, string propertyName)
        {
            if (!parent.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Array || prop.GetArrayLength() == 0)
                return null;

            return prop[0].GetString();
        }

        private static string? GetFirstArrayJoined(JsonElement parent, string propertyName)
        {
            if (!parent.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Array || prop.GetArrayLength() == 0)
                return null;

            var values = new List<string>();

            foreach (var item in prop.EnumerateArray())
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    values.Add(value);
            }

            return values.Count == 0 ? null : string.Join(", ", values);
        }

        private static int? TryGetInt(JsonElement parent, string propertyName)
        {
            if (parent.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number)
                return prop.GetInt32();

            return null;
        }

        private static decimal? TryGetDoubleAsDecimal(JsonElement parent, string propertyName)
        {
            if (parent.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number)
                return (decimal)prop.GetDouble();

            return null;
        }

        private static int? ExtractYear(string? publishedDate)
        {
            if (string.IsNullOrWhiteSpace(publishedDate))
                return null;

            if (publishedDate.Length >= 4 && int.TryParse(publishedDate[..4], out var year))
                return year;

            return null;
        }

        private static string? TryGetString(JsonElement parent, string propertyName)
        {
            if (parent.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();

            return null;
        }

        private static string? FirstNotEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return null;
        }

        private static decimal? NormalizeRating(decimal? rating)
        {
            if (!rating.HasValue)
                return null;

            var value = rating.Value;

            if (value < 0) value = 0;
            if (value > 5) value = 5;

            return Math.Round(value, 2);
        }

        private static string TrimOrEmpty(string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            value = value.Trim();
            return value.Length <= max ? value : value[..max];
        }

        private static string? TrimOrNull(string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            value = value.Trim();
            return value.Length <= max ? value : value[..max];
        }

        private static string? TrimIsbnOrNull(string? isbn)
        {
            if (string.IsNullOrWhiteSpace(isbn))
                return null;

            isbn = isbn.Replace("-", "").Trim();

            return isbn.Length <= 13 ? isbn : isbn[..13];
        }

        private sealed class GoogleBooksExtra
        {
            public string? Description { get; set; }
            public string? Publisher { get; set; }
            public int? PublishedYear { get; set; }
            public int? PageCount { get; set; }
            public string? Language { get; set; }
            public decimal? Rating { get; set; }
        }
    }
}