using System.Net.Http.Headers;
using System.Net.Http.Json;
using BuildingBlocks.Application.MultiTenancy;
using BuildingBlocks.Infrastructure.Authentication;
using Catalog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Infrastructure.Persistence;
using Platform.Infrastructure.Persistence;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;

namespace EndToEndTests.Fixtures;

public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("marketplace_e2e")
        .WithUsername("e2e")
        .WithPassword("e2e")
        .Build();

    private readonly MinioContainer _minio = new MinioBuilder()
        .WithImage("minio/minio:latest")
        .WithUsername("minioadmin")
        .WithPassword("minioadmin")
        .Build();

    public const string TestBucket = "product-images-e2e";

    public static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid BuyerId = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid OtherBuyerId = new("44444444-4444-4444-4444-444444444444");
    public static readonly Guid AdminId = new("33333333-3333-3333-3333-333333333333");

    public static readonly Guid AcmeTenantId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid GlobexTenantId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public Guid WidgetId { get; private set; }
    public Guid GizmoId { get; private set; }
    public Guid DoohickeyId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Marketplace"] = _container.GetConnectionString(),
                ["FeatureFlags:CacheSeconds"] = "1",
                ["Outbox:PollIntervalMilliseconds"] = "200",
                ["Jwt:Issuer"] = "marketplace",
                ["Jwt:Audience"] = "marketplace-clients",
                ["Jwt:LifetimeMinutes"] = "60",
                ["Jwt:KeyId"] = TestKeys.KeyId,
                ["Jwt:PrivateKeyPem"] = TestKeys.PrivateKeyPem,
                ["Jwt:PublicKeyPem"] = TestKeys.PublicKeyPem,
                ["Storage:Provider"] = "S3",
                ["Storage:Endpoint"] = _minio.GetConnectionString(),
                ["Storage:PublicEndpoint"] = _minio.GetConnectionString(),
                ["Storage:AccessKey"] = "minioadmin",
                ["Storage:SecretKey"] = "minioadmin",
                ["Storage:Region"] = "us-east-1",
                ["Storage:Bucket"] = TestBucket,
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await _minio.StartAsync();
        await EnsureBucketAsync();
        _ = Services;
        await ResetAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();
        await _minio.DisposeAsync();
    }

    private async Task EnsureBucketAsync()
    {
        var s3Config = new Amazon.S3.AmazonS3Config
        {
            ServiceURL = _minio.GetConnectionString(),
            ForcePathStyle = true,
            UseHttp = true,
            AuthenticationRegion = "us-east-1",
        };
        var creds = new Amazon.Runtime.BasicAWSCredentials("minioadmin", "minioadmin");
        using var s3 = new Amazon.S3.AmazonS3Client(creds, s3Config);
        try { await s3.PutBucketAsync(TestBucket); } catch { /* already exists */ }
    }

    public string MinioEndpoint => _minio.GetConnectionString();

    /// <summary>
    /// Mints a JWT via the same <see cref="JwtTokenIssuer"/> the API uses and
    /// returns a client that carries it as a Bearer token. Defaults the tenant
    /// to Acme so legacy tests don't have to specify; pass an explicit tenant
    /// for multi-tenancy isolation tests.
    /// </summary>
    public HttpClient ClientFor(string role, Guid userId, Guid? tenantId = null)
    {
        var client = CreateClient();
        using var scope = Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<JwtTokenIssuer>();
        var (token, _) = issuer.Mint(userId, role, tenantId ?? AcmeTenantId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient AnonymousClient() => CreateClient();

    private IServiceScope CreateTenantScope(Guid tenantId)
    {
        var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(tenantId);
        return scope;
    }

    public async Task ResetAsync()
    {
        using var scope = CreateTenantScope(AcmeTenantId);
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var orders = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        await orders.OutboxMessages.ExecuteDeleteAsync();
        await orders.InboxMessages.ExecuteDeleteAsync();
        await orders.Orders.IgnoreQueryFilters().ExecuteDeleteAsync();

        await catalog.OutboxMessages.ExecuteDeleteAsync();
        await catalog.InboxMessages.ExecuteDeleteAsync();
        await catalog.Products.IgnoreQueryFilters().ExecuteDeleteAsync();

        // Reseed Catalog (seeds Acme + Globex products)
        await CatalogDataSeeder.SeedAsync(catalog);

        // Reset Acme's feature flag rollout to 0% and clear user opt-ins
        var flag = await platform.FeatureFlags.IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Id == "EnablePremiumBadge" && f.TenantId == AcmeTenantId);
        if (flag is null)
        {
            await PlatformDataSeeder.SeedAsync(platform);
            flag = await platform.FeatureFlags.IgnoreQueryFilters()
                .FirstAsync(f => f.Id == "EnablePremiumBadge" && f.TenantId == AcmeTenantId);
        }
        flag.SetRolloutPercentage(0, DateTime.UtcNow);
        var collection = typeof(global::Platform.Domain.FeatureFlags.FeatureFlag)
            .GetProperty(nameof(global::Platform.Domain.FeatureFlags.FeatureFlag.EnabledUserIds))!;
        collection.SetValue(flag, new List<Guid>());
        await platform.SaveChangesAsync();

        // Clear the feature-flag cache so the new rollout takes effect immediately.
        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        if (cache is MemoryCache concrete) concrete.Clear();

        // Capture deterministic ids for Acme catalog
        WidgetId = (await catalog.Products.IgnoreQueryFilters().AsNoTracking()
            .FirstAsync(p => p.Name == "Widget" && p.TenantId == AcmeTenantId)).Id.Value;
        GizmoId = (await catalog.Products.IgnoreQueryFilters().AsNoTracking()
            .FirstAsync(p => p.Name == "Gizmo" && p.TenantId == AcmeTenantId)).Id.Value;
        DoohickeyId = (await catalog.Products.IgnoreQueryFilters().AsNoTracking()
            .FirstAsync(p => p.Name == "Doohickey" && p.TenantId == AcmeTenantId)).Id.Value;
    }

    /// <summary>Adds a suspended product so we can hit the "non-published" path.</summary>
    public async Task<Guid> AddSuspendedProductAsync(string name, Guid? tenantId = null)
    {
        using var scope = CreateTenantScope(tenantId ?? AcmeTenantId);
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var p = global::Catalog.Domain.Products.Product
            .Create(name, global::Catalog.Domain.Products.Money.Usd(10m), 5, SellerId, tenantId ?? AcmeTenantId, DateTime.UtcNow).Value;
        p.Suspend();
        p.ClearDomainEvents();
        catalog.Products.Add(p);
        await catalog.SaveChangesAsync();
        return p.Id.Value;
    }

    /// <summary>Polls the buyer order endpoint until status matches or timeout.</summary>
    public async Task<string> WaitForOrderStatusAsync(Guid orderId, Guid asBuyer, string expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var client = ClientFor("Buyer", asBuyer);
        while (DateTime.UtcNow < deadline)
        {
            var resp = await client.GetAsync($"/api/buyer/orders/{orderId}");
            if (resp.IsSuccessStatusCode)
            {
                var dto = await resp.Content.ReadFromJsonAsync<OrderProbe>();
                if (dto?.Status == expected) return expected;
            }
            await Task.Delay(150);
        }
        // Final probe so the assertion message is informative
        var final = await client.GetAsync($"/api/buyer/orders/{orderId}");
        var lastStatus = final.IsSuccessStatusCode ? (await final.Content.ReadFromJsonAsync<OrderProbe>())?.Status : final.StatusCode.ToString();
        return $"<timeout, last status={lastStatus}>";
    }

    public async Task<int> GetProductStockAsync(Guid productId, Guid? tenantId = null)
    {
        using var scope = CreateTenantScope(tenantId ?? AcmeTenantId);
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var id = new global::Catalog.Domain.Products.ProductId(productId);
        var p = await catalog.Products.AsNoTracking().FirstAsync(x => x.Id == id);
        return p.Stock.Value;
    }

    private sealed record OrderProbe(Guid Id, Guid BuyerId, Guid ProductId, int Quantity, string Status, DateTime CreatedAt);
}

[CollectionDefinition(nameof(ApiCollection))]
public class ApiCollection : ICollectionFixture<ApiFixture>;
