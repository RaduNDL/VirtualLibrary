using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VirtualLibrary.Models
{
    public class CheckoutInputModel : IValidatableObject
    {
        [Required]
        [StringLength(200)]
        public string FullName { get; set; }

        [Required]
        [StringLength(200)]
        public string Address { get; set; }

        [Required]
        [StringLength(100)]
        public string City { get; set; }

        [Required]
        [StringLength(20)]
        public string PostalCode { get; set; }

        [Required]
        [StringLength(20)]
        public string PhoneNumber { get; set; }


        [Required]
        [Display(Name = "Card number")]
        [DataType(DataType.CreditCard)]
        [RegularExpression(@"^\d{13}$", ErrorMessage = "Card number must contain only digits (13 digits).")]
        public string CardNumber { get; set; }

        [Required]
        [Display(Name = "Expiry month")]
        [Range(1, 12, ErrorMessage = "Expiry month must be between 1 and 12.")]
        public int ExpiryMonth { get; set; }

        [Required]
        [Display(Name = "Expiry year")]
        [Range(2024, 2100, ErrorMessage = "Expiry year must be between 2024 and 2100.")]
        public int ExpiryYear { get; set; }

        [Required]
        [Display(Name = "CVV")]
        [RegularExpression(@"^\d{3}$", ErrorMessage = "CVV must be 3 digits.")]
        public string Cvv { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var now = DateTime.UtcNow;

            if (ExpiryYear < now.Year ||
               (ExpiryYear == now.Year && ExpiryMonth < now.Month))
            {
                yield return new ValidationResult(
                    "Card has expired.",
                    new[] { nameof(ExpiryMonth), nameof(ExpiryYear) }
                );
            }
        }
    }
}
