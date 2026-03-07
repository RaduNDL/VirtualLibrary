using System.ComponentModel.DataAnnotations.Schema;

namespace VirtualLibrary.Models
{
    public class Audiobook
    {
        public int AudiobookId { get; set; }

        public int ProductId { get; set; }

        public Product Product { get; set; } = null!;

        public AudiobookStatus Status { get; set; } = AudiobookStatus.Pending;

        public string? AudioFilePath { get; set; }

        public string? ErrorMessage { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAtUtc { get; set; }

        public TimeSpan? Duration { get; set; }

        public bool IsCompleted =>
            Status == AudiobookStatus.Completed &&
            !string.IsNullOrWhiteSpace(AudioFilePath);

        public bool HasFailed =>
            Status == AudiobookStatus.Failed;

        public bool CanRetry =>
            Status == AudiobookStatus.Failed ||
            Status == AudiobookStatus.Pending;
    }
}