
using System.ComponentModel.DataAnnotations;
using EcommerceApp.Models;

namespace EcommerceApp.ViewModels
{
    public class CheckoutViewModel
    {
        [Required(ErrorMessage = "Full Name is required")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone Number is required")]
        [Phone(ErrorMessage = "Invalid Phone Number")]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public string City { get; set; } = string.Empty;

        [Required]
        public string Area { get; set; } = string.Empty;

        [Required]
        public string Street { get; set; } = string.Empty;

        [Required]
        public string Building { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;

        public List<EcommerceApp.Models.Address> UserAddresses { get; set; } = new();

        public List<CartItem> CartItems { get; set; } = new();
        public decimal Subtotal => CartItems.Sum(x => x.Total);
        public decimal DeliveryFee { get; set; } = 5.00m;
        public decimal GrandTotal => Subtotal + DeliveryFee;
    }
}
