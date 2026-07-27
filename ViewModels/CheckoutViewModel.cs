
using System.ComponentModel.DataAnnotations;
using EcommerceApp.Models;

namespace EcommerceApp.ViewModels
{
    public class CheckoutViewModel
    {
        [Required(ErrorMessage = "Full Name is required")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone Number is required")]
        [RegularExpression(@"^01[0125]\d{8}$", ErrorMessage = "Invalid Egyptian phone number")]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Area { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Street { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Building { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;

        public List<EcommerceApp.Models.Address> UserAddresses { get; set; } = new();
        public int? SelectedAddressId { get; set; }

        public List<CartItem> CartItems { get; set; } = new();
        public decimal Subtotal => CartItems.Sum(x => x.Total);
        public decimal DeliveryFee { get; set; }
        public decimal GrandTotal => Subtotal + DeliveryFee;

        [Required]
        [StringLength(64, MinimumLength = 32)]
        public string IdempotencyKey { get; set; } = string.Empty;
    }
}
