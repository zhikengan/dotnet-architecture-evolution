using Catalog.Domain.Products;
using Catalog.Domain.Products.Errors;
using Catalog.Domain.Products.Events;

namespace Catalog.UnitTests;

/// <summary>
/// Domain invariants for the Product aggregate — no I/O, no MediatR. These
/// are the "rules" the rest of the system trusts.
/// </summary>
public class ProductTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Seller = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Tenant = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void Create_with_valid_data_raises_ProductCreated()
    {
        var r = Product.Create("Widget", Money.Usd(10m), 50, Seller, Tenant, Now);
        r.IsSuccess.Should().BeTrue();
        r.Value.Status.Should().Be(ProductStatus.Published);
        r.Value.Stock.Value.Should().Be(50);
        r.Value.DomainEvents.Should().ContainSingle(e => e is ProductCreated);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_with_blank_name_fails(string name) =>
        Product.Create(name, Money.Usd(10m), 50, Seller, Tenant, Now).Error.Should().Be(ProductErrors.InvalidName);

    [Fact]
    public void Create_with_too_long_name_fails() =>
        Product.Create(new string('x', 201), Money.Usd(10m), 50, Seller, Tenant, Now)
            .Error.Should().Be(ProductErrors.InvalidName);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_with_non_positive_price_fails(decimal price) =>
        Product.Create("Widget", Money.Usd(price), 50, Seller, Tenant, Now).Error.Should().Be(ProductErrors.InvalidPrice);

    [Fact]
    public void Create_with_empty_tenant_fails() =>
        Product.Create("Widget", Money.Usd(10m), 50, Seller, Guid.Empty, Now).Error.Should().Be(ProductErrors.InvalidTenant);

    [Fact]
    public void Decrement_published_with_sufficient_stock_succeeds()
    {
        var p = Product.Create("Widget", Money.Usd(10m), 10, Seller, Tenant, Now).Value;
        p.ClearDomainEvents();
        p.Decrement(3, Guid.NewGuid()).IsSuccess.Should().BeTrue();
        p.Stock.Value.Should().Be(7);
        p.DomainEvents.Should().ContainSingle(e => e is StockDecremented);
    }

    [Fact]
    public void Decrement_below_zero_raises_StockDecrementFailed_and_keeps_stock()
    {
        var p = Product.Create("Widget", Money.Usd(10m), 5, Seller, Tenant, Now).Value;
        p.ClearDomainEvents();
        p.Decrement(10, Guid.NewGuid()).IsFailure.Should().BeTrue();
        p.Stock.Value.Should().Be(5);
        p.DomainEvents.Should().ContainSingle(e => e is StockDecrementFailed);
    }

    [Fact]
    public void Decrement_suspended_product_raises_StockDecrementFailed_with_NotPublished()
    {
        var p = Product.Create("Widget", Money.Usd(10m), 5, Seller, Tenant, Now).Value;
        p.Suspend();
        p.ClearDomainEvents();
        p.Decrement(1, Guid.NewGuid()).Error.Should().Be(ProductErrors.NotPublished);
    }

    [Fact]
    public void Return_increases_stock_and_raises_StockReturned()
    {
        var p = Product.Create("Widget", Money.Usd(10m), 5, Seller, Tenant, Now).Value;
        p.ClearDomainEvents();
        p.Return(3, Guid.NewGuid()).IsSuccess.Should().BeTrue();
        p.Stock.Value.Should().Be(8);
        p.DomainEvents.Should().ContainSingle(e => e is StockReturned);
    }
}
