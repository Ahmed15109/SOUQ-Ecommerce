
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceApp.Models
{
    public class OrderItem
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public Order Order { get; set; } = null!;

        public int ProductId { get; set; }
        [MaxLength(150)]
        public string ProductName { get; set; } = string.Empty;
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }
        
        [Range(1, 100)]
        public int Quantity { get; set; }

        [MaxLength(500)]
        public string ImageUrl { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? SelectedWeightKg { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? SelectedPricePerKg { get; set; }

        public bool CuttingSelected { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CuttingFeeApplied { get; set; }
    }
}
