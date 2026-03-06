using EdgeTtsSharp;
using Microsoft.EntityFrameworkCore;
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

        public async Task<Audiobook?> GenerateAudiobookAsync(int productId)
        {
            try
            {
                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId);
                if (product == null) return null;

                var existingAudiobook = await _db.Audiobooks
                    .FirstOrDefaultAsync(a => a.ProductId == productId);

                if (existingAudiobook != null && existingAudiobook.Status == AudiobookStatus.Completed)
                    return existingAudiobook;

                Audiobook audiobook;
                if (existingAudiobook != null)
                {
                    audiobook = existingAudiobook;
                    audiobook.Status = AudiobookStatus.Processing;
                    audiobook.ErrorMessage = null;
                }
                else
                {
                    audiobook = new Audiobook
                    {
                        ProductId = productId,
                        Status = AudiobookStatus.Processing,
                        CreatedAtUtc = DateTime.UtcNow
                    };
                    _db.Audiobooks.Add(audiobook);
                }

                await _db.SaveChangesAsync();

                var textToSpeak = await PrepareAudioTextAsync(product);

                if (string.IsNullOrWhiteSpace(textToSpeak))
                {
                    audiobook.Status = AudiobookStatus.Rejected;
                    audiobook.ErrorMessage = "No text content available. The book needs a PDF or description.";
                    await _db.SaveChangesAsync();
                    return audiobook;
                }

                _logger.LogInformation("Generating audiobook for '{Title}' ({Length} chars) using Edge TTS",
                    product.Title, textToSpeak.Length);

                var audioFilePath = await SynthesizeSpeechAsync(productId, textToSpeak);

                if (string.IsNullOrEmpty(audioFilePath))
                {
                    audiobook.Status = AudiobookStatus.Failed;
                    audiobook.ErrorMessage = "Failed to generate audio. Please try again.";
                    await _db.SaveChangesAsync();
                    return audiobook;
                }

                audiobook.AudioFilePath = audioFilePath;
                audiobook.Status = AudiobookStatus.Completed;
                audiobook.CompletedAtUtc = DateTime.UtcNow;

                var wordCount = textToSpeak.Split(new[] { ' ', '\n', '\r' },
                    StringSplitOptions.RemoveEmptyEntries).Length;
                audiobook.Duration = TimeSpan.FromSeconds(wordCount * 0.4);

                await _db.SaveChangesAsync();

                _logger.LogInformation("✓ Audiobook generated for '{Title}'", product.Title);
                return audiobook;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating audiobook for product {ProductId}", productId);

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
            var textParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(product.Title))
                textParts.Add(product.Title);

            if (!string.IsNullOrWhiteSpace(product.Author))
                textParts.Add($"by {product.Author}");

            if (!string.IsNullOrWhiteSpace(product.PdfFilePath))
            {
                var fullPdfPath = Path.Combine(_env.WebRootPath, product.PdfFilePath);
                if (File.Exists(fullPdfPath))
                {
                    var extractedText = await _pdfService.ExtractTextAsync(fullPdfPath);
                    if (!string.IsNullOrWhiteSpace(extractedText))
                        textParts.Add(extractedText);
                }
            }
            else if (!string.IsNullOrWhiteSpace(product.Description))
            {
                textParts.Add(product.Description);
            }

            var fullText = string.Join(". ", textParts);

            if (fullText.Length > 5000)
                fullText = fullText[..5000];

            return fullText;
        }

        /// <summary>
        /// Generate MP3 using EdgeTtsSharp — FREE, no API key, no account needed.
        /// </summary>
        private async Task<string> SynthesizeSpeechAsync(int productId, string text)
        {
            var audioBooksDir = Path.Combine(_env.WebRootPath, "audiobooks");
            if (!Directory.Exists(audioBooksDir))
                Directory.CreateDirectory(audioBooksDir);

            var fileName = $"{productId}_{Guid.NewGuid():N}.mp3";
            var filePath = Path.Combine(audioBooksDir, fileName);

            var voiceName = _config["EdgeTTS:Voice"] ?? "en-US-AriaNeural";

            _logger.LogInformation("Edge TTS: voice={Voice}, output={Path}", voiceName, filePath);

            try
            {
                // Get the voice object
                var voice = await EdgeTts.GetVoice(voiceName);

                if (voice == null)
                {
                    _logger.LogWarning("Voice '{Voice}' not found, falling back to en-US-AriaNeural", voiceName);
                    voice = await EdgeTts.GetVoice("en-US-AriaNeural");
                }

                if (voice == null)
                {
                    _logger.LogError("No Edge TTS voice available!");
                    return string.Empty;
                }

                // Split text into chunks (Edge TTS has limits per request)
                var chunks = SplitTextIntoChunks(text, 2000);
                _logger.LogInformation("Split text into {Count} chunks", chunks.Count);

                if (chunks.Count == 1)
                {
                    // Single chunk — save directly
                    await voice.SaveAudioToFile(chunks[0], filePath);
                }
                else
                {
                    // Multiple chunks — concat into one file
                    var tempFiles = new List<string>();

                    try
                    {
                        for (int i = 0; i < chunks.Count; i++)
                        {
                            if (string.IsNullOrWhiteSpace(chunks[i])) continue;

                            var tempFile = Path.Combine(audioBooksDir, $"temp_{productId}_{i}.mp3");
                            await voice.SaveAudioToFile(chunks[i], tempFile);
                            tempFiles.Add(tempFile);

                            _logger.LogInformation("  Chunk {Index}/{Total} done", i + 1, chunks.Count);
                        }

                        // Concatenate all temp MP3 files into one
                        using var output = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                        foreach (var tempFile in tempFiles)
                        {
                            var bytes = await File.ReadAllBytesAsync(tempFile);
                            await output.WriteAsync(bytes);
                        }
                    }
                    finally
                    {
                        // Clean up temp files
                        foreach (var tempFile in tempFiles)
                        {
                            if (File.Exists(tempFile))
                                File.Delete(tempFile);
                        }
                    }
                }

                var fileSize = new FileInfo(filePath).Length;
                _logger.LogInformation("✓ Audio saved: {Path} ({Size} bytes)", filePath, fileSize);

                if (fileSize < 100)
                {
                    _logger.LogWarning("Audio file too small, probably failed");
                    File.Delete(filePath);
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Edge TTS synthesis failed");

                if (File.Exists(filePath))
                    File.Delete(filePath);

                return string.Empty;
            }

            return Path.Combine("audiobooks", fileName).Replace('\\', '/');
        }

        private static List<string> SplitTextIntoChunks(string text, int maxChunkSize)
        {
            var chunks = new List<string>();
            if (string.IsNullOrEmpty(text)) return chunks;

            var remaining = text;
            while (remaining.Length > 0)
            {
                if (remaining.Length <= maxChunkSize)
                {
                    chunks.Add(remaining);
                    break;
                }

                var splitAt = maxChunkSize;
                var lastPeriod = remaining.LastIndexOf('.', maxChunkSize - 1);
                var lastExclamation = remaining.LastIndexOf('!', maxChunkSize - 1);
                var lastQuestion = remaining.LastIndexOf('?', maxChunkSize - 1);
                var lastNewline = remaining.LastIndexOf('\n', maxChunkSize - 1);

                var bestSplit = new[] { lastPeriod, lastExclamation, lastQuestion, lastNewline }.Max();
                if (bestSplit > maxChunkSize / 2)
                    splitAt = bestSplit + 1;

                chunks.Add(remaining[..splitAt].Trim());
                remaining = remaining[splitAt..].Trim();
            }

            return chunks;
        }

        public async Task<bool> DeleteAudiobookAsync(int audiobookId)
        {
            var audiobook = await _db.Audiobooks.FirstOrDefaultAsync(a => a.AudiobookId == audiobookId);
            if (audiobook == null) return false;

            if (!string.IsNullOrWhiteSpace(audiobook.AudioFilePath))
            {
                var fullPath = Path.Combine(_env.WebRootPath, audiobook.AudioFilePath);
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }

            _db.Audiobooks.Remove(audiobook);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}