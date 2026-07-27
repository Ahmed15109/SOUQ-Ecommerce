
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceApp.Models
{
    public class Product : IValidatableObject
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;
        
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 10000.00)]
        public decimal Price { get; set; }
        
        [MaxLength(500)]
        public string ImageUrl { get; set; } = string.Empty;
        
        public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }

        public bool IsFeatured { get; set; }

        [NotMapped]
        public bool IsFavorite { get; set; }

        public SellingMode SellingMode { get; set; } = SellingMode.Normal;

        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 1000)]
        public decimal? MinKg { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 1000)]
        public decimal? MaxKg { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 1000)]
        public decimal? StepKg { get; set; }

        public bool AllowCutting { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 10000)]
        public decimal CuttingFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 10000)]
        public decimal PricePerKg { get; set; }

        public ICollection<ProductWeightTier> WeightTiers { get; set; } = new List<ProductWeightTier>();
        public ICollection<UserFavorite> UserFavorites { get; set; } = new List<UserFavorite>();

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (SellingMode == SellingMode.Normal && Price <= 0)
            {
                yield return new ValidationResult(
                    "Price must be greater than zero for normally priced products.",
                    [nameof(Price)]);
            }

            if (SellingMode == SellingMode.ByWeight)
            {
                if (!MinKg.HasValue || !MaxKg.HasValue || !StepKg.HasValue)
                {
                    yield return new ValidationResult(
                        "Minimum weight, maximum weight, and weight step are required for weighted products.",
                        new[] { nameof(MinKg), nameof(MaxKg), nameof(StepKg) });
                }
                else
                {
                    if (MinKg.Value >= MaxKg.Value)
                    {
                        yield return new ValidationResult(
                            "Maximum weight must be greater than minimum weight.",
                            new[] { nameof(MinKg), nameof(MaxKg) });
                    }

                    if (StepKg.Value > MaxKg.Value - MinKg.Value)
                    {
                        yield return new ValidationResult(
                            "Weight step cannot exceed the configured weight range.",
                            new[] { nameof(StepKg) });
                    }
                }

                if (PricePerKg <= 0)
                {
                    yield return new ValidationResult(
                        "Price per kilogram must be greater than zero.",
                        new[] { nameof(PricePerKg) });
                }
            }
        }
    }
}
