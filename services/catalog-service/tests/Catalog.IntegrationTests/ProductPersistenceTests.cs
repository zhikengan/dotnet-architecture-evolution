using Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Catalog.IntegrationTests;

[Collection(nameof(CatalogDbCollection))]
public class ProductPersistenceTests(CatalogDbFixture fx)
{
    [Fact]
    public async Task Product_round_trips_through_EF_with_owned_Money_value()
    {
        var seller = Guid.NewGuid();
        var product = Product.Create("Widget", Money.Usd(12.50m), 25, seller, CatalogDbFixture.AcmeTenant, DateTime.UtcNow).Value;
        product.ClearDomainEvents();

        await using (var db = fx.NewContext(CatalogDbFixture.AcmeTenant))
        {
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        await using var read = fx.NewContext(CatalogDbFixture.AcmeTenant);
        var loaded = await read.Products.SingleAsync(p => p.Id == product.Id);
        loaded.Name.Should().Be("Widget");
        loaded.Price.Amount.Should().Be(12.50m);
        loaded.Price.Currency.Should().Be(Money.UsdCode);
        loaded.Stock.Value.Should().Be(25);
        loaded.Status.Should().Be(ProductStatus.Published);
    }

    [Fact]
    public async Task Tenant_query_filter_isolates_rows_across_tenants()
    {
        var globex = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var acmeProduct = Product.Create("AcmeOnly", Money.Usd(1m), 1, Guid.NewGuid(), CatalogDbFixture.AcmeTenant, DateTime.UtcNow).Value;
        var globexProduct = Product.Create("GlobexOnly", Money.Usd(1m), 1, Guid.NewGuid(), globex, DateTime.UtcNow).Value;
        acmeProduct.ClearDomainEvents();
        globexProduct.ClearDomainEvents();

        await using (var db = fx.NewContext(CatalogDbFixture.AcmeTenant))
        {
            db.Products.AddRange(acmeProduct, globexProduct);
            await db.SaveChangesAsync();
        }

        await using var asAcme = fx.NewContext(CatalogDbFixture.AcmeTenant);
        var visible = await asAcme.Products.Where(p => p.Name.EndsWith("Only")).Select(p => p.Name).ToListAsync();
        visible.Should().Contain("AcmeOnly").And.NotContain("GlobexOnly");

        await using var asGlobex = fx.NewContext(globex);
        var globexVisible = await asGlobex.Products.Where(p => p.Name.EndsWith("Only")).Select(p => p.Name).ToListAsync();
        globexVisible.Should().Contain("GlobexOnly").And.NotContain("AcmeOnly");
    }
}

[CollectionDefinition(nameof(CatalogDbCollection))]
public class CatalogDbCollection : ICollectionFixture<CatalogDbFixture>;
