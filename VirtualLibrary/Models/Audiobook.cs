using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VirtualLibrary.Models
{
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
        public string Status { get; set; } = "Pending";

        public TimeSpan? Duration { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAtUtc { get; set; }

        [StringLength(1000)]
        public string? ErrorMessage { get; set; }
    }
}