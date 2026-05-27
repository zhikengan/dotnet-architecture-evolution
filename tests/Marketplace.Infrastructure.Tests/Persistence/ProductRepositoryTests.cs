using Marketplace.Domain.Products;
using Marketplace.Infrastructure.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.Tests.Persistence;

[Collection(nameof(DatabaseCollection))]
public class ProductRepositoryTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fx;

    public ProductRepositoryTests(DatabaseFixture fx) => _fx = fx;

    public Task InitializeAsync() => _fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Product_round_trips_value_objects_correctly()
    {
        var sellerId = Guid.NewGuid();
        var product = Product.Create("Widget", Money.Usd(12.50m), 25, sellerId, DateTime.UtcNow).Value;
        product.ClearDomainEvents();

        await using (var write = _fx.CreateContext())
        {
            write.Products.Add(product);
            await write.SaveChangesAsync();
        }

        await using var read = _fx.CreateContext();
        var loaded = await read.Products.SingleAsync();

        loaded.Name.Should().Be("Widget");
        loaded.Price.Amount.Should().Be(12.50m);
        loaded.Price.Currency.Should().Be("USD");
        loaded.Stock.Value.Should().Be(25);
        loaded.Status.Should().Be(ProductStatus.Published);
        loaded.SellerId.Should().Be(sellerId);
    }

    [Fact]
    public async Task Buyer_query_filters_to_published_only()
    {
        await using (var write = _fx.CreateContext())
        {
            var published = Product.Create("Published", Money.Usd(10m), 5, Guid.NewGuid(), DateTime.UtcNow).Value;
            var suspended = Product.Create("Suspended", Money.Usd(10m), 5, Guid.NewGuid(), DateTime.UtcNow).Value;
            suspended.Suspend();
            published.ClearDomainEvents();
            suspended.ClearDomainEvents();
            write.Products.AddRange(published, suspended);
            await write.SaveChangesAsync();
        }

        await using var read = _fx.CreateContext();
        var visible = await read.Products.Where(p => p.Status == ProductStatus.Published).ToListAsync();

        visible.Should().HaveCount(1);
        visible[0].Name.Should().Be("Published");
    }
}
