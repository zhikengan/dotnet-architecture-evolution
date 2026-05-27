using Marketplace.Domain.Products;
using Marketplace.Domain.Products.Events;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Persistence.Interceptors;
using Marketplace.Infrastructure.Tests.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Marketplace.Infrastructure.Tests.Persistence;

[Collection(nameof(DatabaseCollection))]
public class DomainEventDispatchInterceptorTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fx;

    public DomainEventDispatchInterceptorTests(DatabaseFixture fx) => _fx = fx;

    public Task InitializeAsync() => _fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SaveChanges_publishes_each_aggregate_domain_event_then_clears_them()
    {
        var publisher = Substitute.For<IPublisher>();
        var interceptor = new DomainEventDispatchInterceptor(publisher);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fx.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new AppDbContext(options);
        var product = Product.Create("Test", Money.Usd(10m), 5, Guid.NewGuid(), DateTime.UtcNow).Value;
        db.Products.Add(product);

        product.DomainEvents.Should().ContainSingle(e => e is ProductCreated);

        await db.SaveChangesAsync();

        await publisher.Received(1).Publish(
            Arg.Is<object>(e => e is ProductCreated),
            Arg.Any<CancellationToken>());

        product.DomainEvents.Should().BeEmpty();
    }
}
