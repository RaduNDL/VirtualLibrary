using System.Text.Json;

namespace VirtualLibrary.Services
{
    public class BookMetadataEnricher
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<BookMetadataEnricher> _logger;

        public BookMetadataEnricher(
            IHttpClientFactory httpClientFactory,
            ILogger<BookMetadataEnricher> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public Task<BookMetadataResult?> GetFromGoogleBooksAsync(string? isbn)
        {
            if (string.IsNullOrWhiteSpace(isbn))
                return Task.FromResult<BookMetadataResult?>(null);

            return SearchGoogleBooksAsync($"isbn:{isbn.Trim()}");
        }

        public async Task<BookMetadataResult?> GetBestMetadataAsync(string? isbn, string title, string? author)
        {
            var byIsbn = await GetFromGoogleBooksAsync(isbn);
            if (HasUsefulData(byIsbn))
                return byIsbn;

            if (string.IsNullOrWhiteSpace(title))
                return byIsbn;

            var queryParts = new List<string>
            {
                $"intitle:{title.Trim()}"
            };

            if (!string.IsNullOrWhiteSpace(author))
                queryParts.Add($"inauthor:{author.Trim()}");

            var byTitle = await SearchGoogleBooksAsync(string.Join(" ", queryParts));
            return HasUsefulData(byTitle) ? byTitle : byIsbn;
        }

        private async Task<BookMetadataResult?> SearchGoogleBooksAsync(string query)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("PdfClient");
                var url =
                    $"https://www.googleapis.com/books/v1/volumes?q={Uri.EscapeDataString(query)}&maxResults=1&printType=books";

                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var root = doc.RootElement;
                if (!root.TryGetProperty("items", out var items) ||
                    items.ValueKind != JsonValueKind.Array ||
                    items.GetArrayLength() == 0)
                {
                    return null;
                }

                var firstItem = items[0];
                if (!firstItem.TryGetProperty("volumeInfo", out var volumeInfo))
                    return null;

                return ParseBookMetadata(volumeInfo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enrich metadata from Google Books for query {Query}", query);
                return null;
            }
        }

        private static BookMetadataResult ParseBookMetadata(JsonElement volumeInfo)
        {
            string? publisher = volumeInfo.TryGetProperty("publisher", out var pubProp)
                ? pubProp.GetString()
                : null;

            string? publishedDate = volumeInfo.TryGetProperty("publishedDate", out var publishedDateProp)
                ? publishedDateProp.GetString()
                : null;

            int? publishedYear = ParseYear(publishedDate);

            int? pageCount = null;
            if (volumeInfo.TryGetProperty("pageCount", out var pageCountProp) &&
                pageCountProp.ValueKind == JsonValueKind.Number)
            {
                pageCount = pageCountProp.GetInt32();
            }

            string? language = volumeInfo.TryGetProperty("language", out var languageProp)
                ? languageProp.GetString()
                : null;

            decimal? rating = null;
            if (volumeInfo.TryGetProperty("averageRating", out var ratingProp) &&
                ratingProp.ValueKind == JsonValueKind.Number)
            {
                rating = Convert.ToDecimal(ratingProp.GetDouble());
            }

            string? description = volumeInfo.TryGetProperty("description", out var descriptionProp)
                ? descriptionProp.GetString()
                : null;

            return new BookMetadataResult
            {
                Publisher = publisher,
                PublishedYear = publishedYear,
                PageCount = pageCount,
                Language = language,
                Rating = rating,
                Description = description
            };
        }

        private static bool HasUsefulData(BookMetadataResult? result)
        {
            if (result == null)
                return false;

            return !string.IsNullOrWhiteSpace(result.Description) ||
                   !string.IsNullOrWhiteSpace(result.Publisher) ||
                   result.PageCount.HasValue ||
                   result.PublishedYear.HasValue ||
                   !string.IsNullOrWhiteSpace(result.Language) ||
                   result.Rating.HasValue;
        }

        private static int? ParseYear(string? input)
        {
            if (string.IsNullOrWhiteSpace(input) || input.Length < 4)
                return null;

            return int.TryParse(input.Substring(0, 4), out var year) ? year : null;
        }
    }
}