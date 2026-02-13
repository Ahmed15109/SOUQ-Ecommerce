
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceApp.Models
{
    public class CartItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; } = string.Empty;

        public decimal Total => Price * Quantity;

        public decimal? SelectedWeightKg { get; set; }
        public decimal? SelectedPricePerKg { get; set; }
        public bool? CuttingSelected { get; set; }
        public decimal CuttingFeeApplied { get; set; }
    }
}
