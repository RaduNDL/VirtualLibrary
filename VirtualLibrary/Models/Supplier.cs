using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VirtualLibrary.Models
{
    public class Supplier
    {
        [Key]
        public int SupplierId { get; set; }

        [Required(ErrorMessage = "Supplier name is required.")]
        [StringLength(500, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 500 characters.")]
        [Display(Name = "Supplier Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "Contact info cannot exceed 200 characters.")]
        [Display(Name = "Contact Info")]
        public string? ContactInfo { get; set; }

        [StringLength(300, ErrorMessage = "Address cannot exceed 300 characters.")]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}