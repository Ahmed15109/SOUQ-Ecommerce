namespace EcommerceApp.ViewModels
{
    public class OrderItemSuccessVm
    {
        public string ProductName { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal { get; set; }
        public decimal? SelectedWeightKg { get; set; }
        public decimal? SelectedPricePerKg { get; set; }
        public bool CuttingSelected { get; set; }
        public decimal CuttingFeeApplied { get; set; }
    }
}
