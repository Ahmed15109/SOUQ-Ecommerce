using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceApp.Models
{
    public class ProductWeightTier
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Required]
        public decimal FromKg { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Required]
        public decimal ToKg { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Required]
        public decimal PricePerKg { get; set; }
    }
}
