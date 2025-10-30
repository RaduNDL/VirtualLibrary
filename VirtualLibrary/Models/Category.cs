using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace VirtualLibrary.Models
{
    public class Category
    {
        public int CategoryId { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [ValidateNever]                             
        public ICollection<Product>? Products { get; set; } = new List<Product>(); 
    }
}
