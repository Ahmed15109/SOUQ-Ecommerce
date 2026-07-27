using System.ComponentModel.DataAnnotations;
using System.Text;
using EcommerceApp.Models;
using EcommerceApp.Services;
using QuestPDF.Infrastructure;

namespace EcommerceApp.Tests;

public sealed class DomainSafetyTests
{
    [Fact]
    public void ProductValidation_RequiresThePriceForItsSellingMode()
    {
        var normalProduct = new Product
        {
            Name = "منتج عادي",
            Description = string.Empty,
            SellingMode = SellingMode.Normal,
            Price = 0
        };
        var weightedProduct = new Product
        {
            Name = "منتج بالوزن",
            Description = string.Empty,
            SellingMode = SellingMode.ByWeight,
            Price = 0,
            MinKg = 0.5m,
            MaxKg = 2m,
            StepKg = 0.25m,
            PricePerKg = 75m
        };

        var normalResults = Validate(normalProduct);
        var weightedResults = Validate(weightedProduct);

        Assert.Contains(normalResults, result =>
            result.MemberNames.Contains(nameof(Product.Price)));
        Assert.Empty(weightedResults);
    }

    [Fact]
    public void ArabicInvoice_UsesPackagedFontAndProducesACompletePdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var fontPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Fonts",
            "Cairo-Regular.ttf");
        Assert.True(File.Exists(fontPath), $"Required test font was not copied to '{fontPath}'.");

        using (var fontStream = File.OpenRead(fontPath))
        {
            QuestPDF.Drawing.FontManager.RegisterFont(fontStream);
        }

        var order = new Order
        {
            Id = 42,
            FullName = "أحمد محمد",
            Phone = "01000000000",
            City = "القاهرة",
            Area = "مدينة نصر",
            Street = "شارع السوق",
            Building = "١٠",
            Notes = "تسليم مساءً",
            Subtotal = 150m,
            DeliveryFee = 15m,
            Total = 165m,
            CreatedAt = DateTime.UtcNow,
            OrderItems =
            [
                new OrderItem
                {
                    ProductName = "خضروات طازجة",
                    UnitPrice = 75m,
                    Quantity = 2,
                    LineTotal = 150m
                }
            ]
        };

        var pdf = new PdfInvoiceService().GenerateInvoice(order);

        Assert.True(pdf.Length > 10_000);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        Assert.Contains(
            "%%EOF",
            Encoding.ASCII.GetString(pdf, Math.Max(0, pdf.Length - 64), Math.Min(64, pdf.Length)));
    }

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);
        return results;
    }
}
