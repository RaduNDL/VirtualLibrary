using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VirtualLibrary.Models
{
    public class Supplier
    {
        [Key]
        public int SupplierId { get; set; }

        [Required(ErrorMessage = "Supplier name is required.")]
        [StringLength(500, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters.")]
        [RegularExpression(@"^[a-zA-ZăâîșțĂÂÎȘȚ\s\-\.&']+$",
            ErrorMessage = "Name can only contain letters, spaces, and - . & ' characters. No digits allowed.")]
        [Display(Name = "Supplier Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(200, MinimumLength = 7, ErrorMessage = "Phone number must be between 7 and 20 characters.")]
        [RegularExpression(@"^[\+]?[0-9\s\-\(\)]+$",
            ErrorMessage = "Contact Info must contain only digits, spaces, and + - ( ) characters. No letters allowed.")]
        [Display(Name = "Contact Info")]
        public string? ContactInfo { get; set; }

        [StringLength(300, ErrorMessage = "Address cannot exceed 300 characters.")]
        [RegularExpression(@"^[a-zA-ZăâîșțĂÂÎȘȚ0-9\s\,\.\-\/\#]+$",
            ErrorMessage = "Address can only contain letters, digits, spaces, and , . - / # characters.")]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}