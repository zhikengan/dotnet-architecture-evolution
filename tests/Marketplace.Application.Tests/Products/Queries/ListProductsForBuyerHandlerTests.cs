using Marketplace.Application.Products.Queries.ListProductsForBuyer;
using Marketplace.Application.Tests.Common;
using Marketplace.Application.Tests.Common.Builders;

namespace Marketplace.Application.Tests.Products.Queries;

public class ListProductsForBuyerHandlerTests : TestBase
{
    [Fact]
    public async Task Returns_only_published_products_with_InStock_flag()
    {
        DbContext.Products.Add(new ProductBuilder().WithName("Widget").WithStock(5).Build());
        DbContext.Products.Add(new ProductBuilder().WithName("Doohickey").WithStock(0).Build());
        DbContext.Products.Add(new ProductBuilder().WithName("Suspended").WithStock(10).Suspended().Build());
        await DbContext.SaveChangesAsync();

        var handler = new ListProductsForBuyerHandler(DbContext);

        var result = await handler.Handle(new ListProductsForBuyerQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().NotContain(p => p.Name == "Suspended");
        result.Value.Single(p => p.Name == "Widget").InStock.Should().BeTrue();
        result.Value.Single(p => p.Name == "Doohickey").InStock.Should().BeFalse();
    }

    [Fact]
    public async Task Returns_empty_when_no_published_products()
    {
        DbContext.Products.Add(new ProductBuilder().WithName("Suspended").Suspended().Build());
        await DbContext.SaveChangesAsync();

        var handler = new ListProductsForBuyerHandler(DbContext);
        var result = await handler.Handle(new ListProductsForBuyerQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
