
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceApp.Models
{
    public class Product
    {
        public int Id { get; set; }
        
        [Required]
        public string Name { get; set; } = string.Empty;
        
        public string Description { get; set; } = string.Empty;
        
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 10000.00)]
        public decimal Price { get; set; }
        
        public string ImageUrl { get; set; } = string.Empty;
        
        public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }

        public bool IsFavorite { get; set; }

        // New fields for ByWeight logic
        public SellingMode SellingMode { get; set; } = SellingMode.Normal;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MinKg { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaxKg { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? StepKg { get; set; }

        public bool AllowCutting { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CuttingFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PricePerKg { get; set; }

        public ICollection<ProductWeightTier> WeightTiers { get; set; } = new List<ProductWeightTier>();
    }
}
