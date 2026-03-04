using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VirtualLibrary.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(200)]
        [Display(Name = "Title")]
        public string Title { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "Author")]
        public string? Author { get; set; }

        [StringLength(13)]
        [Display(Name = "ISBN")]
        public string? Isbn { get; set; }

        [StringLength(4000)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Range(0, 10000)]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Price (RON)")]
        public decimal Price { get; set; }

        [Display(Name = "Stock")]
        public int Stock { get; set; }

        [StringLength(500)]
        [Display(Name = "Image Path")]
        public string? ImagePath { get; set; }

        [StringLength(500)]
        [Display(Name = "PDF File Path")]
        public string? PdfFilePath { get; set; }

        [StringLength(50)]
        [Display(Name = "PDF Source")]
        public string? PdfSource { get; set; } 

        public int? CategoryId { get; set; }
        public Category? Category { get; set; }

        public int? SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        [Display(Name = "Created At")]
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [Display(Name = "Updated At")]
        public DateTime? UpdatedAtUtc { get; set; }

        [NotMapped]
        public bool HasPdfAvailable => !string.IsNullOrWhiteSpace(PdfFilePath);
    }
}