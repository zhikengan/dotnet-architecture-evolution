using Marketplace.Application.Products.Queries.ListProductsForAdmin;
using Marketplace.Application.Tests.Common;
using Marketplace.Application.Tests.Common.Builders;

namespace Marketplace.Application.Tests.Products.Queries;

public class ListProductsForAdminHandlerTests : TestBase
{
    [Fact]
    public async Task Returns_all_products_including_suspended()
    {
        DbContext.Products.Add(new ProductBuilder().WithName("Widget").Build());
        DbContext.Products.Add(new ProductBuilder().WithName("Suspended").Suspended().Build());
        await DbContext.SaveChangesAsync();

        var handler = new ListProductsForAdminHandler(DbContext);

        var result = await handler.Handle(new ListProductsForAdminQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(p => p.Name == "Suspended");
    }
}
