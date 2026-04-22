using QuestPDF.Fluent;
using QuestPDF.Helpers;
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
            if (product == null)
                throw new ArgumentNullException(nameof(product));

            var pdfDir = Path.Combine(_env.WebRootPath, "pdfs", "descriptions");
            Directory.CreateDirectory(pdfDir);

            var safeId = product.Id > 0 ? product.Id.ToString() : Guid.NewGuid().ToString("N");
            var fileName = $"desc_{safeId}_{Guid.NewGuid():N}.pdf";
            var fullPath = Path.Combine(pdfDir, fileName);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Column(header =>
                    {
                        header.Spacing(6);

                        header.Item().Text(text =>
                        {
                            text.Span("VirtualLibrary - Book Details")
                                .FontSize(22)
                                .Bold()
                                .FontColor(Colors.Blue.Darken2);
                        });

                        header.Item().Text(text =>
                        {
                            text.Span($"Generated on: {DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC")
                                .FontSize(10)
                                .FontColor(Colors.Grey.Darken1);
                        });

                        header.Item()
                            .PaddingTop(4)
                            .LineHorizontal(1)
                            .LineColor(Colors.Grey.Lighten1);
                    });

                    page.Content().Column(col =>
                    {
                        col.Spacing(12);

                        col.Item().Text(text =>
                        {
                            text.Span(product.Title ?? "Untitled")
                                .FontSize(20)
                                .Bold();
                        });

                        if (!string.IsNullOrWhiteSpace(product.Author))
                        {
                            col.Item().Text(text =>
                            {
                                text.Span("Author: ").Bold();
                                text.Span(product.Author);
                            });
                        }

                        col.Item().PaddingTop(4).Text(text =>
                        {
                            text.Span("Book Information")
                                .FontSize(14)
                                .Bold()
                                .FontColor(Colors.Blue.Medium);
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(150);
                                columns.RelativeColumn();
                            });

                            void AddRow(string label, string? value)
                            {
                                if (string.IsNullOrWhiteSpace(value))
                                    return;

                                table.Cell().PaddingVertical(4).Text(text =>
                                {
                                    text.Span(label).Bold();
                                });

                                table.Cell().PaddingVertical(4).Text(value);
                            }

                            AddRow("Title", product.Title);
                            AddRow("Author", product.Author);
                            AddRow("Category", product.Category?.Name ?? "Uncategorized");
                            AddRow("ISBN", product.Isbn);
                            AddRow("Publisher", product.Publisher);
                            AddRow("Published year", product.PublishedYear?.ToString());
                            AddRow("Page count", product.PageCount?.ToString());
                            AddRow("Language", product.Language);
                            AddRow("Rating", product.Rating?.ToString("0.0"));
                            AddRow("Price", $"{product.Price:0.00} RON");
                            AddRow("Supplier", product.Supplier?.Name);
                            AddRow("Created at", product.CreatedAtUtc.ToString("dd.MM.yyyy HH:mm 'UTC'"));
                            AddRow("Updated at", product.UpdatedAtUtc?.ToString("dd.MM.yyyy HH:mm 'UTC'"));
                        });

                        col.Item().PaddingTop(8).Text(text =>
                        {
                            text.Span("Description")
                                .FontSize(14)
                                .Bold()
                                .FontColor(Colors.Blue.Medium);
                        });

                        col.Item()
                            .Border(1)
                            .BorderColor(Colors.Grey.Lighten2)
                            .Padding(12)
                            .Text(text =>
                            {
                                text.Span(product.Description ?? "No description available.")
                                    .LineHeight(1.4f);
                            });

                       

                      
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("VirtualLibrary").FontSize(10).FontColor(Colors.Grey.Darken1);
                        text.Span(" • ").FontSize(10).FontColor(Colors.Grey.Darken1);
                        text.Span("Generated description/specification PDF").FontSize(10).FontColor(Colors.Grey.Darken1);
                    });
                });
            }).GeneratePdf(fullPath);

            var relativePath = Path.Combine("pdfs", "descriptions", fileName).Replace('\\', '/');
            return Task.FromResult<string?>(relativePath);
        }
    }
}