using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace VirtualLibrary.Services
{
    public class AudiobookWorker : BackgroundService
    {
        private readonly AudiobookQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AudiobookWorker> _logger;

        public AudiobookWorker(
            AudiobookQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<AudiobookWorker> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AudiobookWorker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var productId = await _queue.DequeueAsync(stoppingToken);

                    using var scope = _scopeFactory.CreateScope();
                    var audiobookService = scope.ServiceProvider.GetRequiredService<AudiobookService>();

                    await audiobookService.GenerateAudiobookAsync(productId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Audiobook worker error.");
                }
            }
        }
    }
}