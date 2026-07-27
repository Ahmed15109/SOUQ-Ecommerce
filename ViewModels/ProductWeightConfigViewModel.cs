using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.ViewModels
{
    public class ProductWeightConfigViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductImageUrl { get; set; } = string.Empty;
        
        public decimal? MinKg { get; set; }
        public decimal? MaxKg { get; set; }
        public decimal? StepKg { get; set; }
        public bool AllowCutting { get; set; }
        public decimal CuttingFee { get; set; }
        
        public decimal PricePerKg { get; set; }
        public List<WeightTierPriceViewModel> WeightTiers { get; set; } = [];

        [Required(ErrorMessage = "يرجى إدخال الوزن")]
        [Display(Name = "الوزن (كجم)")]
        public decimal SelectedWeight { get; set; }

        [Display(Name = "تنظيف وتقطيع")]
        public bool IsCuttingRequested { get; set; }

    }

    public class WeightTierPriceViewModel
    {
        public decimal FromKg { get; set; }
        public decimal ToKg { get; set; }
        public decimal PricePerKg { get; set; }
    }
}
