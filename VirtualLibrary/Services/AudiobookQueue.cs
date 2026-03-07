using System.Threading.Channels;

namespace VirtualLibrary.Services
{
    public class AudiobookQueue
    {
        private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

        public async ValueTask EnqueueAsync(int productId)
        {
            await _channel.Writer.WriteAsync(productId);
        }

        public async ValueTask<int> DequeueAsync(CancellationToken ct)
        {
            return await _channel.Reader.ReadAsync(ct);
        }
    }
}