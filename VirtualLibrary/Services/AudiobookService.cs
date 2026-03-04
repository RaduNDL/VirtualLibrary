using Google.Cloud.TextToSpeech.V1;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Services
{
    public class AudiobookService
    {
        private readonly TextToSpeechClient _ttsClient;
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly ILogger<AudiobookService> _logger;

        public AudiobookService(
            AppDbContext db,
            IWebHostEnvironment env,
            IConfiguration config,
            ILogger<AudiobookService> logger)
        {
            _db = db;
            _env = env;
            _config = config;
            _logger = logger;

            try
            {
                _ttsClient = TextToSpeechClient.Create();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initializing TTS client: {ex.Message}");
                throw;
            }
        }

        public async Task<Audiobook?> GenerateAudiobookAsync(int productId)
        {
            try
            {
                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId);
                if (product == null)
                {
                    _logger.LogWarning($"Product {productId} not found");
                    return null;
                }

                var existingAudiobook = await _db.Audiobooks
                    .FirstOrDefaultAsync(a => a.ProductId == productId);

                if (existingAudiobook != null && existingAudiobook.Status == "Completed")
                {
                    _logger.LogInformation($"Audiobook for product {productId} already exists");
                    return existingAudiobook;
                }

                Audiobook audiobook;
                if (existingAudiobook != null)
                {
                    audiobook = existingAudiobook;
                    audiobook.Status = "Processing";
                    audiobook.ErrorMessage = null;
                }
                else
                {
                    audiobook = new Audiobook
                    {
                        ProductId = productId,
                        Status = "Processing",
                        CreatedAtUtc = DateTime.UtcNow
                    };
                    _db.Audiobooks.Add(audiobook);
                }

                await _db.SaveChangesAsync();

                var textToSpeak = PrepareAudioText(product);

                if (string.IsNullOrWhiteSpace(textToSpeak))
                {
                    audiobook.Status = "Failed";
                    audiobook.ErrorMessage = "No text content available for audio generation";
                    await _db.SaveChangesAsync();
                    return audiobook;
                }

                var audioContent = await SynthesizeSpeechAsync(textToSpeak);

                var audioFilePath = await SaveAudioFileAsync(productId, audioContent);

                audiobook.AudioFilePath = audioFilePath;
                audiobook.Status = "Completed";
                audiobook.CompletedAtUtc = DateTime.UtcNow;

                var wordCount = textToSpeak.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
                audiobook.Duration = TimeSpan.FromSeconds(wordCount * 0.4);

                await _db.SaveChangesAsync();

                _logger.LogInformation($"Successfully generated audiobook for product {productId}");
                return audiobook;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating audiobook: {ex.Message}");

                var audiobook = await _db.Audiobooks.FirstOrDefaultAsync(a => a.ProductId == productId);
                if (audiobook != null)
                {
                    audiobook.Status = "Failed";
                    audiobook.ErrorMessage = ex.Message;
                    await _db.SaveChangesAsync();
                }

                throw;
            }
        }

        private string PrepareAudioText(Product product)
        {
            var textParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(product.Title))
                textParts.Add($"Titlu: {product.Title}");

            if (!string.IsNullOrWhiteSpace(product.Author))
                textParts.Add($"Autor: {product.Author}");

            if (!string.IsNullOrWhiteSpace(product.Description))
                textParts.Add(product.Description);

            return string.Join(". ", textParts);
        }

        private async Task<byte[]> SynthesizeSpeechAsync(string text)
        {
            try
            {
                var input = new SynthesisInput { Text = text };

                var voice = new VoiceSelectionParams
                {
                    LanguageCode = _config["GoogleCloud:TextToSpeechSettings:Language"] ?? "ro-RO",
                    Name = _config["GoogleCloud:TextToSpeechSettings:VoiceName"] ?? "ro-RO-Neural2-A"
                };

                var audioConfig = new AudioConfig
                {
                    AudioEncoding = AudioEncoding.Mp3,
                    SpeakingRate = double.Parse(_config["GoogleCloud:TextToSpeechSettings:SpeakingRate"] ?? "1.0"),
                    Pitch = double.Parse(_config["GoogleCloud:TextToSpeechSettings:Pitch"] ?? "0.0")
                };

                var response = await _ttsClient.SynthesizeSpeechAsync(input, voice, audioConfig);
                return response.AudioContent.ToByteArray();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error synthesizing speech: {ex.Message}");
                throw;
            }
        }

        private async Task<string> SaveAudioFileAsync(int productId, byte[] audioContent)
        {
            try
            {
                var audioBooksDir = Path.Combine(_env.WebRootPath, "audiobooks");

                if (!Directory.Exists(audioBooksDir))
                    Directory.CreateDirectory(audioBooksDir);

                var fileName = $"{productId}_{Guid.NewGuid():N}.mp3";
                var filePath = Path.Combine(audioBooksDir, fileName);

                await File.WriteAllBytesAsync(filePath, audioContent);

                return Path.Combine("audiobooks", fileName).Replace('\\', '/');
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving audio file: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> DeleteAudiobookAsync(int audiobookId)
        {
            try
            {
                var audiobook = await _db.Audiobooks.FirstOrDefaultAsync(a => a.AudiobookId == audiobookId);

                if (audiobook == null)
                    return false;

                if (!string.IsNullOrWhiteSpace(audiobook.AudioFilePath))
                {
                    var fullPath = Path.Combine(_env.WebRootPath, audiobook.AudioFilePath);
                    if (File.Exists(fullPath))
                        File.Delete(fullPath);
                }

                _db.Audiobooks.Remove(audiobook);
                await _db.SaveChangesAsync();

                _logger.LogInformation($"Audiobook {audiobookId} deleted successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting audiobook: {ex.Message}");
                return false;
            }
        }
    }
}