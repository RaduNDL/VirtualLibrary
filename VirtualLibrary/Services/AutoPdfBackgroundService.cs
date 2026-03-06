using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;

namespace VirtualLibrary.Services
{
    public class AutoPdfBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AutoPdfBackgroundService> _logger;

        public AutoPdfBackgroundService(IServiceProvider serviceProvider, ILogger<AutoPdfBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var pdfService = scope.ServiceProvider.GetRequiredService<PdfService>();

                    var booksWithoutPdf = await db.Products
                        .Where(p => string.IsNullOrEmpty(p.PdfFilePath) && p.PdfSource != "Not Found")
                        .Take(5)
                        .ToListAsync(stoppingToken);

                    if (booksWithoutPdf.Any())
                    {
                        _logger.LogInformation("Auto-PDF worker: processing {Count} books", booksWithoutPdf.Count);
                    }

                    foreach (var book in booksWithoutPdf)
                    {
                        try
                        {
                            var pdfUrl = await pdfService.SearchOpenLibraryPdfAsync(
                                book.Isbn, book.Title, book.Author ?? "");

                            if (!string.IsNullOrEmpty(pdfUrl))
                            {
                                var savedPath = await pdfService.DownloadAndSavePdfAsync(
                                    book.Id, pdfUrl, "Auto-Worker");
                                if (!string.IsNullOrEmpty(savedPath))
                                {
                                    book.PdfFilePath = savedPath;
                                    book.PdfSource = "Auto-Worker";
                                    _logger.LogInformation("Auto-PDF: found PDF for '{Title}'", book.Title);
                                }
                                else
                                {
                                    book.PdfSource = "Not Found";
                                }
                            }
                            else
                            {
                                book.PdfSource = "Not Found";
                            }

                            await db.SaveChangesAsync(stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Auto-PDF: error for '{Title}'", book.Title);
                        }

                        await Task.Delay(3000, stoppingToken);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto-PDF worker error");
                }

                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
        }
    }
}