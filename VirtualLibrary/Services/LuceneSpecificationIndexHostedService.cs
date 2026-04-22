using Microsoft.Extensions.DependencyInjection;

namespace VirtualLibrary.Services
{
    public sealed class LuceneSpecificationIndexHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LuceneSpecificationIndexHostedService> _logger;

        public LuceneSpecificationIndexHostedService(
            IServiceProvider serviceProvider,
            ILogger<LuceneSpecificationIndexHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var luceneService = scope.ServiceProvider.GetRequiredService<LuceneSpecificationSearchService>();

                await luceneService.RebuildIndexAsync();

                _logger.LogInformation("Lucene specification index rebuilt successfully at startup.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rebuild Lucene specification index at startup.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}