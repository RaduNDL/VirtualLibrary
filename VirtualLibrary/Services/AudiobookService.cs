using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Speech.Synthesis;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Services
{
    public class AudiobookService
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly PdfService _pdfService;
        private readonly ILogger<AudiobookService> _logger;

        public AudiobookService(
            AppDbContext db,
            IWebHostEnvironment env,
            IConfiguration config,
            PdfService pdfService,
            ILogger<AudiobookService> logger)
        {
            _db = db;
            _env = env;
            _config = config;
            _pdfService = pdfService;
            _logger = logger;
        }

        public async Task<Audiobook?> StartGenerationAsync(int productId)
        {
            var productExists = await _db.Products
                .AsNoTracking()
                .AnyAsync(p => p.Id == productId);

            if (!productExists)
                return null;

            var audiobook = await _db.Audiobooks
                .FirstOrDefaultAsync(a => a.ProductId == productId);

            if (audiobook != null)
            {
                if (audiobook.Status == AudiobookStatus.Completed && AudioFileExists(audiobook))
                    return audiobook;

                if (!string.IsNullOrWhiteSpace(audiobook.AudioFilePath))
                {
                    var oldFullPath = Path.Combine(_env.ContentRootPath, audiobook.AudioFilePath);
                    DeleteFileSafe(oldFullPath);
                }

                audiobook.Status = AudiobookStatus.Pending;
                audiobook.AudioFilePath = null;
                audiobook.ErrorMessage = null;
                audiobook.CompletedAtUtc = null;
                audiobook.Duration = null;
            }
            else
            {
                audiobook = new Audiobook
                {
                    ProductId = productId,
                    Status = AudiobookStatus.Pending,
                    CreatedAtUtc = DateTime.UtcNow
                };

                _db.Audiobooks.Add(audiobook);
            }

            await _db.SaveChangesAsync();
            return audiobook;
        }

        public async Task<Audiobook?> GenerateAudiobookAsync(int productId)
        {
            try
            {
                var product = await _db.Products
                    .FirstOrDefaultAsync(p => p.Id == productId);

                if (product == null)
                    return null;

                var audiobook = await _db.Audiobooks
                    .FirstOrDefaultAsync(a => a.ProductId == productId);

                if (audiobook != null && audiobook.Status == AudiobookStatus.Completed && AudioFileExists(audiobook))
                    return audiobook;

                if (audiobook == null)
                {
                    audiobook = new Audiobook
                    {
                        ProductId = productId,
                        Status = AudiobookStatus.Processing,
                        CreatedAtUtc = DateTime.UtcNow
                    };

                    _db.Audiobooks.Add(audiobook);
                }
                else
                {
                    audiobook.Status = AudiobookStatus.Processing;
                    audiobook.ErrorMessage = null;
                    audiobook.CompletedAtUtc = null;
                    audiobook.AudioFilePath = null;
                    audiobook.Duration = null;
                }

                await _db.SaveChangesAsync();

                var textToSpeak = await PrepareAudioTextAsync(product);

                if (string.IsNullOrWhiteSpace(textToSpeak))
                {
                    audiobook.Status = AudiobookStatus.Failed;
                    audiobook.ErrorMessage = "No readable text was found.";
                    await _db.SaveChangesAsync();
                    return audiobook;
                }

                var audioRelativePath = await SynthesizeSpeechAsync(productId, textToSpeak);

                if (string.IsNullOrWhiteSpace(audioRelativePath))
                {
                    audiobook.Status = AudiobookStatus.Failed;
                    audiobook.ErrorMessage = "Audio generation failed.";
                    await _db.SaveChangesAsync();
                    return audiobook;
                }

                audiobook.AudioFilePath = audioRelativePath;
                audiobook.Status = AudiobookStatus.Completed;
                audiobook.CompletedAtUtc = DateTime.UtcNow;

                var wordCount = textToSpeak.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
                audiobook.Duration = TimeSpan.FromSeconds(Math.Max(3, wordCount * 0.42));

                await _db.SaveChangesAsync();
                return audiobook;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audiobook generation failed for product {ProductId}", productId);

                try
                {
                    var audiobook = await _db.Audiobooks.FirstOrDefaultAsync(a => a.ProductId == productId);
                    if (audiobook != null)
                    {
                        audiobook.Status = AudiobookStatus.Failed;
                        audiobook.ErrorMessage = ex.Message;
                        await _db.SaveChangesAsync();
                    }

                    return audiobook;
                }
                catch
                {
                    return null;
                }
            }
        }

        private async Task<string> PrepareAudioTextAsync(Product product)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(product.Title))
                parts.Add(product.Title);

            if (!string.IsNullOrWhiteSpace(product.Author))
                parts.Add($"by {product.Author}");

            string? extractedText = null;

            if (!string.IsNullOrWhiteSpace(product.PdfFilePath))
            {
                var fullPdfPath = Path.Combine(_env.WebRootPath, product.PdfFilePath);

                if (File.Exists(fullPdfPath))
                {
                    try
                    {
                        extractedText = await _pdfService.ExtractTextAsync(fullPdfPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "PDF extraction failed for product {ProductId}", product.Id);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(extractedText))
                parts.Add(extractedText);
            else if (!string.IsNullOrWhiteSpace(product.Description))
                parts.Add(product.Description);

            var fullText = string.Join(". ", parts);

            fullText = Regex.Replace(fullText, "<.*?>", string.Empty);
            fullText = Regex.Replace(fullText, @"[\p{C}-[\r\n\t]]+", "");
            fullText = Regex.Replace(fullText, @"\s+", " ").Trim();

            return fullText;
        }
        private async Task<string> SynthesizeSpeechAsync(int productId, string text)
        {
            var outputDir = Path.Combine(_env.ContentRootPath, "Generated", "Audiobooks");
            Directory.CreateDirectory(outputDir);

            var fileName = $"{productId}_{Guid.NewGuid():N}.wav";
            var finalPath = Path.Combine(outputDir, fileName);

            return await Task.Run(() =>
            {
                try
                {
                    using var synthesizer = new SpeechSynthesizer();
                    synthesizer.SetOutputToWaveFile(finalPath);
                    synthesizer.Speak(text);

                    var finalLength = new FileInfo(finalPath).Length;
                    if (finalLength < 100)
                    {
                        DeleteFileSafe(finalPath);
                        return string.Empty;
                    }

                    return Path.Combine("Generated", "Audiobooks", fileName).Replace('\\', '/');
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Speech synthesis failed for product {ProductId}", productId);
                    DeleteFileSafe(finalPath);
                    return string.Empty;
                }
            });
        }

        private bool AudioFileExists(Audiobook audiobook)
        {
            if (string.IsNullOrWhiteSpace(audiobook.AudioFilePath))
                return false;

            var fullPath = Path.Combine(_env.ContentRootPath, audiobook.AudioFilePath);
            return File.Exists(fullPath);
        }

        private static void DeleteFileSafe(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        public async Task<bool> DeleteAudiobookAsync(int audiobookId)
        {
            var audiobook = await _db.Audiobooks.FirstOrDefaultAsync(a => a.AudiobookId == audiobookId);
            if (audiobook == null)
                return false;

            if (!string.IsNullOrWhiteSpace(audiobook.AudioFilePath))
            {
                var fullPath = Path.Combine(_env.ContentRootPath, audiobook.AudioFilePath);
                DeleteFileSafe(fullPath);
            }

            _db.Audiobooks.Remove(audiobook);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}