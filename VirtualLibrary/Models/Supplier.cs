using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VirtualLibrary.Models
{
    public class Supplier
    {
        [Key]
        public int SupplierId { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? ContactInfo { get; set; }

        [StringLength(100)]
        public string? Address { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
