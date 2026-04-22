using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VirtualLibrary.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Author { get; set; }

        [StringLength(13)]
        public string? Isbn { get; set; }

        [StringLength(4000)]
        public string? Description { get; set; }

        [StringLength(200)]
        public string? Publisher { get; set; }

        public int? PublishedYear { get; set; }

        public int? PageCount { get; set; }

        [StringLength(50)]
        public string? Language { get; set; }

        [Range(0, 5)]
        [Column(TypeName = "decimal(3,2)")]
        public decimal? Rating { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [StringLength(500)]
        public string? ImagePath { get; set; }

   
        [StringLength(500)]
        public string? DescriptionPdfPath { get; set; }

        public int? CategoryId { get; set; }
        public Category? Category { get; set; }

        public int? SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }

        [NotMapped]
        public bool HasDescriptionPdf => !string.IsNullOrWhiteSpace(DescriptionPdfPath);
    }
}