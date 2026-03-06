using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;

namespace VirtualLibrary.Services
{
    public class AutoPdfBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public AutoPdfBackgroundService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var pdfService = scope.ServiceProvider.GetRequiredService<PdfService>();

                    var booksWithoutPdf = await db.Products
                        .Where(p => string.IsNullOrEmpty(p.PdfFilePath) && p.PdfSource != "Not Found")
                        .Take(3)
                        .ToListAsync(stoppingToken);

                    foreach (var book in booksWithoutPdf)
                    {
                        var pdfUrl = await pdfService.SearchOpenLibraryPdfAsync(book.Isbn, book.Title, book.Author ?? "");

                        if (!string.IsNullOrEmpty(pdfUrl))
                        {
                            var savedPath = await pdfService.DownloadAndSavePdfAsync(book.Id, pdfUrl, "Auto-Worker");
                            if (!string.IsNullOrEmpty(savedPath))
                            {
                                book.PdfFilePath = savedPath;
                                book.PdfSource = "Auto-Worker";
                                await db.SaveChangesAsync(stoppingToken);
                            }
                        }
                        else
                        {
                            book.PdfSource = "Not Found";
                            await db.SaveChangesAsync(stoppingToken);
                        }

                        await Task.Delay(3000, stoppingToken);
                    }
                }
                catch
                {
                }

                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
        }
    }
}