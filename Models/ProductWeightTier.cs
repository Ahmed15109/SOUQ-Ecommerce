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
        [Range(0.01, 1000)]
        public decimal FromKg { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Required]
        [Range(0.01, 1000)]
        public decimal ToKg { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Required]
        [Range(0.01, 10000)]
        public decimal PricePerKg { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}
