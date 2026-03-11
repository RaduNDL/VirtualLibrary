using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using VirtualLibrary.Models;

namespace VirtualLibrary.Services
{
    public class BookPdfGenerator
    {
        private readonly IWebHostEnvironment _env;

        public BookPdfGenerator(IWebHostEnvironment env)
        {
            _env = env;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public Task<string?> GenerateBookDescriptionPdfAsync(Product product)
        {
            var pdfDir = Path.Combine(_env.WebRootPath, "pdfs", "descriptions");
            Directory.CreateDirectory(pdfDir);

            var fileName = $"desc_{product.Id}_{Guid.NewGuid():N}.pdf";
            var fullPath = Path.Combine(pdfDir, fileName);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(36);

                    page.Header()
                        .Text("Book Details")
                        .FontSize(20)
                        .Bold();

                    page.Content().Column(col =>
                    {
                        col.Spacing(10);

                        col.Item().Text(product.Title ?? "Untitled").FontSize(18).Bold();

                        if (!string.IsNullOrWhiteSpace(product.Author))
                            col.Item().Text($"Author: {product.Author}");

                        col.Item().Text($"Category: {product.Category?.Name ?? "Uncategorized"}");

                        if (!string.IsNullOrWhiteSpace(product.Isbn))
                            col.Item().Text($"ISBN: {product.Isbn}");

                        if (!string.IsNullOrWhiteSpace(product.Publisher))
                            col.Item().Text($"Publisher: {product.Publisher}");

                        if (product.PublishedYear.HasValue)
                            col.Item().Text($"Published year: {product.PublishedYear.Value}");

                        if (product.PageCount.HasValue)
                            col.Item().Text($"Page count: {product.PageCount.Value}");

                        if (!string.IsNullOrWhiteSpace(product.Language))
                            col.Item().Text($"Language: {product.Language}");

                        if (product.Rating.HasValue)
                            col.Item().Text($"Rating: {product.Rating.Value:0.0}");

                        col.Item().Text($"Price: {product.Price:0.00} RON");

                        col.Item().PaddingTop(10).Text("Description").Bold();
                        col.Item().Text(product.Description ?? "No description available.");
                    });

                    page.Footer()
                        .AlignCenter()
                        .Text("Virtual Library")
                        .FontSize(10);
                });
            }).GeneratePdf(fullPath);

            var relativePath = Path.Combine("pdfs", "descriptions", fileName).Replace('\\', '/');
            return Task.FromResult<string?>(relativePath);
        }
    }
}