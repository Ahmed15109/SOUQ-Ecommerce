using EcommerceApp.Models;

namespace EcommerceApp.Services
{
    public sealed record ProductPriceResult(
        bool IsValid,
        string? ErrorMessage,
        decimal UnitPrice,
        decimal? SelectedWeightKg,
        decimal? SelectedPricePerKg,
        bool CuttingSelected,
        decimal CuttingFeeApplied);

    public interface IProductPricingService
    {
        ProductPriceResult Calculate(Product product, decimal? selectedWeightKg = null, bool cuttingSelected = false);
    }

    public class ProductPricingService : IProductPricingService
    {
        public ProductPriceResult Calculate(Product product, decimal? selectedWeightKg = null, bool cuttingSelected = false)
        {
            if (product.SellingMode != SellingMode.ByWeight)
            {
                return new ProductPriceResult(
                    true,
                    null,
                    product.Price,
                    null,
                    null,
                    false,
                    0m);
            }

            if (!selectedWeightKg.HasValue ||
                !product.MinKg.HasValue ||
                !product.MaxKg.HasValue ||
                !product.StepKg.HasValue ||
                product.MinKg <= 0 ||
                product.MaxKg <= product.MinKg ||
                product.StepKg <= 0)
            {
                return Invalid("The weighted-product configuration is incomplete.");
            }

            var weight = selectedWeightKg.Value;
            if (weight < product.MinKg.Value || weight > product.MaxKg.Value)
            {
                return Invalid("The selected weight is outside the allowed range.");
            }

            var stepOffset = weight - product.MinKg.Value;
            if (stepOffset % product.StepKg.Value != 0)
            {
                return Invalid("The selected weight does not match the configured weight step.");
            }

            var tier = product.WeightTiers
                .Where(t => weight >= t.FromKg && weight <= t.ToKg)
                .OrderByDescending(t => t.FromKg)
                .FirstOrDefault();

            var pricePerKg = tier?.PricePerKg ?? product.PricePerKg;
            if (pricePerKg <= 0)
            {
                return Invalid("The product does not have a valid price per kilogram.");
            }

            var applyCutting = cuttingSelected && product.AllowCutting;
            var cuttingFee = applyCutting ? product.CuttingFee : 0m;
            if (cuttingFee < 0)
            {
                return Invalid("The cutting fee cannot be negative.");
            }

            var unitPrice = checked((weight * pricePerKg) + cuttingFee);
            return new ProductPriceResult(
                true,
                null,
                unitPrice,
                weight,
                pricePerKg,
                applyCutting,
                cuttingFee);
        }

        private static ProductPriceResult Invalid(string message) =>
            new(false, message, 0m, null, null, false, 0m);
    }
}
