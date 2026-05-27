using BuildingBlocks.Infrastructure.Outbox;
using Catalog.Contracts.IntegrationEvents;
using Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Catalog.IntegrationTests;

[Collection(nameof(CatalogDbCollection))]
public class CatalogPersistenceTests(CatalogDbFixture fx) : IAsyncLifetime
{
    public Task InitializeAsync() => fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Product_round_trips_value_objects()
    {
        var seller = Guid.NewGuid();
        var product = Product.Create("Widget", Money.Usd(12.50m), 25, seller, CatalogDbFixture.AcmeTenantId, DateTime.UtcNow).Value;
        product.ClearDomainEvents();

        await using (var db = fx.CreateContext())
        {
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        await using var read = fx.CreateContext();
        var loaded = await read.Products.SingleAsync();
        loaded.Name.Should().Be("Widget");
        loaded.Price.Amount.Should().Be(12.50m);
        loaded.Stock.Value.Should().Be(25);
        loaded.Status.Should().Be(ProductStatus.Published);
    }

    [Fact]
    public async Task OutboxMessage_Enqueue_persists_integration_event_payload_as_jsonb()
    {
        var evt = new ProductCreatedIntegrationEvent(
            Guid.NewGuid(), DateTime.UtcNow, CatalogDbFixture.AcmeTenantId, Guid.NewGuid(), "Test", 9.99m, 100, Guid.NewGuid());

        await using (var db = fx.CreateContext())
        {
            db.OutboxMessages.Enqueue(evt);
            await db.SaveChangesAsync();
        }

        await using var read = fx.CreateContext();
        var row = await read.OutboxMessages.SingleAsync();
        row.Type.Should().Contain(nameof(ProductCreatedIntegrationEvent));
        row.Payload.Should().Contain("\"Test\"");
        row.ProcessedAt.Should().BeNull();
        row.RetryCount.Should().Be(0);
    }

    [Fact]
    public async Task Buyer_query_filters_to_Published_products_only()
    {
        await using (var db = fx.CreateContext())
        {
            var published = Product.Create("Pub", Money.Usd(10m), 5, Guid.NewGuid(), CatalogDbFixture.AcmeTenantId, DateTime.UtcNow).Value;
            var suspended = Product.Create("Susp", Money.Usd(10m), 5, Guid.NewGuid(), CatalogDbFixture.AcmeTenantId, DateTime.UtcNow).Value;
            suspended.Suspend();
            published.ClearDomainEvents();
            suspended.ClearDomainEvents();
            db.Products.AddRange(published, suspended);
            await db.SaveChangesAsync();
        }

        await using var read = fx.CreateContext();
        var visible = await read.Products.Where(p => p.Status == ProductStatus.Published).ToListAsync();
        visible.Should().HaveCount(1);
        visible[0].Name.Should().Be("Pub");
    }
}
