using EcommerceApp.Models;
using EcommerceApp.Services;

namespace EcommerceApp.Tests;

public class ProductPricingServiceTests
{
    private readonly ProductPricingService _service = new();

    [Fact]
    public void NormalProduct_UsesCatalogPriceAndIgnoresWeightOptions()
    {
        var product = new Product
        {
            Price = 42.50m,
            SellingMode = SellingMode.Normal,
            AllowCutting = true,
            CuttingFee = 10m
        };

        var result = _service.Calculate(product, 2m, cuttingSelected: true);

        Assert.True(result.IsValid);
        Assert.Equal(42.50m, result.UnitPrice);
        Assert.Null(result.SelectedWeightKg);
        Assert.False(result.CuttingSelected);
        Assert.Equal(0m, result.CuttingFeeApplied);
    }

    [Fact]
    public void WeightedProduct_UsesMatchingTierAndCuttingFee()
    {
        var product = CreateWeightedProduct();
        product.WeightTiers.Add(new ProductWeightTier
        {
            FromKg = 1m,
            ToKg = 2m,
            PricePerKg = 80m
        });

        var result = _service.Calculate(product, 1.5m, cuttingSelected: true);

        Assert.True(result.IsValid);
        Assert.Equal(1.5m, result.SelectedWeightKg);
        Assert.Equal(80m, result.SelectedPricePerKg);
        Assert.True(result.CuttingSelected);
        Assert.Equal(5m, result.CuttingFeeApplied);
        Assert.Equal(125m, result.UnitPrice);
    }

    [Theory]
    [InlineData("0.25")]
    [InlineData("2.75")]
    [InlineData("1.10")]
    public void WeightedProduct_RejectsOutOfRangeOrUnalignedWeights(string value)
    {
        var result = _service.Calculate(
            CreateWeightedProduct(),
            decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture));

        Assert.False(result.IsValid);
        Assert.Equal(0m, result.UnitPrice);
    }

    private static Product CreateWeightedProduct() =>
        new()
        {
            Price = 100m,
            SellingMode = SellingMode.ByWeight,
            MinKg = 0.5m,
            MaxKg = 2.5m,
            StepKg = 0.25m,
            PricePerKg = 100m,
            AllowCutting = true,
            CuttingFee = 5m
        };
}
