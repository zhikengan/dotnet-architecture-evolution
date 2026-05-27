using Marketplace.Application.Products.CreateProduct;
using Marketplace.Application.Tests.Common;
using Marketplace.Domain.Products;
using Marketplace.Domain.Products.Errors;

namespace Marketplace.Application.Tests.Products.CreateProduct;

public class CreateProductHandlerTests : TestBase
{
    private static readonly Guid Seller = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task With_valid_data_persists_product_and_returns_success()
    {
        var handler = new CreateProductHandler(DbContext, Clock, UnitOfWork);
        var cmd = new CreateProductCommand("Widget", 15m, 10, Seller);

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Widget");
        result.Value.Status.Should().Be(ProductStatus.Published.Name);
        DbContext.Products.Should().ContainSingle();
    }

    [Fact]
    public async Task With_invalid_name_returns_failure_and_persists_nothing()
    {
        var handler = new CreateProductHandler(DbContext, Clock, UnitOfWork);
        var cmd = new CreateProductCommand("", 15m, 10, Seller);

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.InvalidName);
        DbContext.Products.Should().BeEmpty();
    }

    [Fact]
    public async Task With_non_positive_price_returns_InvalidPrice()
    {
        var handler = new CreateProductHandler(DbContext, Clock, UnitOfWork);
        var cmd = new CreateProductCommand("Widget", 0m, 10, Seller);

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.Error.Should().Be(ProductErrors.InvalidPrice);
    }
}
