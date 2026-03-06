using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VirtualLibrary.Models
{
   
    public enum AudiobookStatus
    {
        Pending,

        Processing,

        Completed,

        Failed,

        Rejected,

        Cancelled
    }

    public class Audiobook
    {
        [Key]
        public int AudiobookId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        [StringLength(500)]
        public string? AudioFilePath { get; set; }

       
        [StringLength(50)]
        public AudiobookStatus Status { get; set; } = AudiobookStatus.Pending;

        public TimeSpan? Duration { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAtUtc { get; set; }

        [StringLength(1000)]
        public string? ErrorMessage { get; set; }


        [NotMapped]
        public bool IsCompleted => Status == AudiobookStatus.Completed;

        [NotMapped]
        public bool IsProcessing => Status == AudiobookStatus.Processing;

        [NotMapped]
        public bool HasFailed => Status == AudiobookStatus.Failed;

        [NotMapped]
        public bool CanRetry => Status == AudiobookStatus.Failed
                             || Status == AudiobookStatus.Rejected
                             || Status == AudiobookStatus.Cancelled;
    }
}