using EcommerceApp.Extensions;

namespace EcommerceApp.Tests;

public class PaginationExtensionsTests
{
    [Fact]
    public async Task EmptyResult_UsesFirstPage()
    {
        var result = await Array.Empty<int>()
            .AsQueryable()
            .ToPagedListAsync(pageNumber: int.MaxValue, pageSize: 20);

        Assert.Empty(result.Items);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task PageAndSize_AreClampedToAvailableAndConfiguredLimits()
    {
        var result = await Enumerable.Range(1, 250)
            .AsQueryable()
            .ToPagedListAsync(pageNumber: 99, pageSize: 500, maxPageSize: 100);

        Assert.Equal(3, result.PageNumber);
        Assert.Equal(100, result.PageSize);
        Assert.Equal(250, result.TotalCount);
        Assert.Equal(Enumerable.Range(201, 50), result.Items);
    }
}
