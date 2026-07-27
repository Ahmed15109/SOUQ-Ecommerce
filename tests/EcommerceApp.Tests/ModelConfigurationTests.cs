using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EcommerceApp.Tests;

public class ModelConfigurationTests
{
    [Fact]
    public void CartLineIndexes_SeparateNormalAndWeightedProducts()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(DbCartItem));

        Assert.NotNull(entity);
        var indexes = entity!.GetIndexes().ToDictionary(index => index.GetDatabaseName()!);

        Assert.Equal("[SelectedWeightKg] IS NULL", indexes["IX_DbCartItems_NormalLine"].GetFilter());
        Assert.True(indexes["IX_DbCartItems_NormalLine"].IsUnique);
        Assert.Equal("[SelectedWeightKg] IS NOT NULL", indexes["IX_DbCartItems_WeightedLine"].GetFilter());
        Assert.True(indexes["IX_DbCartItems_WeightedLine"].IsUnique);
    }

    [Fact]
    public void IdempotencyIndexes_AreScopedToTheirOwner()
    {
        using var context = CreateContext();

        var orderIndex = context.Model.FindEntityType(typeof(Order))!
            .GetIndexes()
            .Single(index => index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(Order.UserId), nameof(Order.IdempotencyKey)]));
        var pharmacyIndex = context.Model.FindEntityType(typeof(PharmacyRequest))!
            .GetIndexes()
            .Single(index => index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(PharmacyRequest.UserId), nameof(PharmacyRequest.SubmissionToken)]));

        Assert.True(orderIndex.IsUnique);
        Assert.True(pharmacyIndex.IsUnique);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
