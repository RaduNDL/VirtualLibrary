using Google.Cloud.TextToSpeech.V1;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Models;

namespace VirtualLibrary.Services
{
    public class AudiobookService
    {
        private TextToSpeechClient? _ttsClient;
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly PdfService _pdfService;

        public AudiobookService(
            AppDbContext db,
            IWebHostEnvironment env,
            IConfiguration config,
            PdfService pdfService)
        {
            _db = db;
            _env = env;
            _config = config;
            _pdfService = pdfService;
        }

        private TextToSpeechClient GetTtsClient()
        {
            if (_ttsClient != null)
                return _ttsClient;

            var credentialsPath = _config["GoogleCloud:CredentialsPath"];

            if (!string.IsNullOrWhiteSpace(credentialsPath) && File.Exists(credentialsPath))
            {
                _ttsClient = new TextToSpeechClientBuilder
                {
                    CredentialsPath = credentialsPath
                }.Build();
            }
            else
            {
                _ttsClient = TextToSpeechClient.Create();
            }

            return _ttsClient;
        }

        public async Task<Audiobook?> GenerateAudiobookAsync(int productId)
        {
            try
            {
                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId);
                if (product == null)
                {
                    return null;
                }

                var existingAudiobook = await _db.Audiobooks
                    .FirstOrDefaultAsync(a => a.ProductId == productId);

                if (existingAudiobook != null && existingAudiobook.Status == "Completed")
                {
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

                var textToSpeak = await PrepareAudioTextAsync(product);

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

                return audiobook;
            }
            catch (Exception ex)
            {
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

        private async Task<string> PrepareAudioTextAsync(Product product)
        {
            var textParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(product.Title))
                textParts.Add(product.Title);

            if (!string.IsNullOrWhiteSpace(product.Author))
                textParts.Add(product.Author);

            if (!string.IsNullOrWhiteSpace(product.PdfFilePath))
            {
                var fullPdfPath = Path.Combine(_env.WebRootPath, product.PdfFilePath);
                var extractedText = await _pdfService.ExtractTextAsync(fullPdfPath);

                if (!string.IsNullOrWhiteSpace(extractedText))
                {
                    textParts.Add(extractedText);
                }
            }
            else if (!string.IsNullOrWhiteSpace(product.Description))
            {
                textParts.Add(product.Description);
            }

            var fullText = string.Join(". ", textParts);

            if (fullText.Length > 4000)
            {
                fullText = fullText.Substring(0, 4000);
            }

            return fullText;
        }

        private async Task<byte[]> SynthesizeSpeechAsync(string text)
        {
            var client = GetTtsClient();

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

            var response = await client.SynthesizeSpeechAsync(input, voice, audioConfig);
            return response.AudioContent.ToByteArray();
        }

        private async Task<string> SaveAudioFileAsync(int productId, byte[] audioContent)
        {
            var audioBooksDir = Path.Combine(_env.WebRootPath, "audiobooks");

            if (!Directory.Exists(audioBooksDir))
                Directory.CreateDirectory(audioBooksDir);

            var fileName = $"{productId}_{Guid.NewGuid():N}.mp3";
            var filePath = Path.Combine(audioBooksDir, fileName);

            await File.WriteAllBytesAsync(filePath, audioContent);

            return Path.Combine("audiobooks", fileName).Replace('\\', '/');
        }

        public async Task<bool> DeleteAudiobookAsync(int audiobookId)
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

            return true;
        }
    }
}