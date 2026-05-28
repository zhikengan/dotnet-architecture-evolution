using System.Reflection;
using NetArchTest.Rules;

namespace RepoArchitectureTests;

/// <summary>
/// Repo-wide architectural invariants for Tier 5: services are physically
/// independent. The only legal way to know about another service is through
/// its <c>*.Contracts</c> assembly (integration events + gRPC proto contracts).
/// Reaching into another service's Domain / Application / Infrastructure /
/// Api / Worker means you've built a distributed monolith — fail the build.
/// </summary>
public class ServiceIndependenceTests
{
    private static readonly Assembly CatalogApi = typeof(global::Catalog.Api.GrpcServices.CatalogGrpcService).Assembly;
    private static readonly Assembly CatalogWorker = typeof(Catalog.Worker.WorkerMarker).Assembly;
    private static readonly Assembly OrdersApi = typeof(global::Orders.Api.GrpcServices.OrdersGrpcService).Assembly;
    private static readonly Assembly OrdersWorker = typeof(Orders.Worker.WorkerMarker).Assembly;
    private static readonly Assembly IdentityApi = typeof(global::Identity.Api.GrpcServices.IdentityGrpcService).Assembly;
    private static readonly Assembly NotificationsApi = typeof(global::Notifications.Api.AssemblyMarker).Assembly;
    private static readonly Assembly NotificationsWorker = typeof(Notifications.Worker.WorkerMarker).Assembly;
    private static readonly Assembly PlatformApi = typeof(global::Platform.Api.GrpcServices.PlatformGrpcService).Assembly;

    private static readonly string[] CatalogImplPrefixes = ["Catalog.Domain", "Catalog.Application", "Catalog.Infrastructure", "Catalog.Api", "Catalog.Worker"];
    private static readonly string[] OrdersImplPrefixes = ["Orders.Domain", "Orders.Application", "Orders.Infrastructure", "Orders.Api", "Orders.Worker"];
    private static readonly string[] IdentityImplPrefixes = ["Identity.Domain", "Identity.Application", "Identity.Infrastructure", "Identity.Api"];
    private static readonly string[] NotificationsImplPrefixes = ["Notifications.Domain", "Notifications.Application", "Notifications.Infrastructure", "Notifications.Api", "Notifications.Worker"];
    private static readonly string[] PlatformImplPrefixes = ["Platform.Domain", "Platform.Application", "Platform.Infrastructure", "Platform.Api"];

    public static IEnumerable<object[]> NonCatalogServiceAssemblies() =>
    [
        [OrdersApi, "Orders.Api"],
        [OrdersWorker, "Orders.Worker"],
        [IdentityApi, "Identity.Api"],
        [NotificationsApi, "Notifications.Api"],
        [NotificationsWorker, "Notifications.Worker"],
        [PlatformApi, "Platform.Api"],
    ];

    [Theory]
    [MemberData(nameof(NonCatalogServiceAssemblies))]
    public void Services_must_not_reference_Catalog_impl(Assembly asm, string name) =>
        AssertNoImplDependency(asm, name, "Catalog", CatalogImplPrefixes);

    public static IEnumerable<object[]> NonOrdersServiceAssemblies() =>
    [
        [CatalogApi, "Catalog.Api"],
        [CatalogWorker, "Catalog.Worker"],
        [IdentityApi, "Identity.Api"],
        [NotificationsApi, "Notifications.Api"],
        [NotificationsWorker, "Notifications.Worker"],
        [PlatformApi, "Platform.Api"],
    ];

    [Theory]
    [MemberData(nameof(NonOrdersServiceAssemblies))]
    public void Services_must_not_reference_Orders_impl(Assembly asm, string name) =>
        AssertNoImplDependency(asm, name, "Orders", OrdersImplPrefixes);

    [Fact]
    public void Notifications_must_not_reach_into_Catalog_or_Platform_impl()
    {
        AssertNoImplDependency(NotificationsApi, "Notifications.Api", "Catalog", CatalogImplPrefixes);
        AssertNoImplDependency(NotificationsApi, "Notifications.Api", "Platform", PlatformImplPrefixes);
        AssertNoImplDependency(NotificationsWorker, "Notifications.Worker", "Catalog", CatalogImplPrefixes);
        AssertNoImplDependency(NotificationsWorker, "Notifications.Worker", "Platform", PlatformImplPrefixes);
    }

    [Fact]
    public void BFFs_must_not_reach_into_any_service_impl()
    {
        var bffs = new[]
        {
            typeof(BuyerBff.Marker).Assembly,
            typeof(SellerBff.Marker).Assembly,
            typeof(AdminBff.Marker).Assembly,
        };
        var allImpl = CatalogImplPrefixes
            .Concat(OrdersImplPrefixes)
            .Concat(IdentityImplPrefixes)
            .Concat(NotificationsImplPrefixes)
            .Concat(PlatformImplPrefixes)
            .ToArray();
        foreach (var bff in bffs)
        {
            AssertNoImplDependency(bff, bff.GetName().Name ?? "BFF", "any service", allImpl);
        }
    }

    [Fact]
    public void Contracts_projects_must_not_depend_on_their_service_impl()
    {
        // Contracts are the published surface — they MUST not pull in EF/MediatR
        // wiring or service-internal types. They depend only on BuildingBlocks
        // (for IIntegrationEvent) and possibly gRPC tooling.
        var contracts = new (Assembly Asm, string Name, string[] ForbiddenPrefixes)[]
        {
            (typeof(Catalog.Contracts.IntegrationEvents.ProductCreatedIntegrationEvent).Assembly, "Catalog.Contracts", CatalogImplPrefixes),
            (typeof(Orders.Contracts.IntegrationEvents.OrderPlacedIntegrationEvent).Assembly, "Orders.Contracts", OrdersImplPrefixes),
            (typeof(Notifications.Contracts.IntegrationEvents.NotificationSentIntegrationEvent).Assembly, "Notifications.Contracts", NotificationsImplPrefixes),
            (typeof(Platform.Contracts.IntegrationEvents.FeatureFlagToggledIntegrationEvent).Assembly, "Platform.Contracts", PlatformImplPrefixes),
            (typeof(Identity.Contracts.IntegrationEvents.UserCreatedIntegrationEvent).Assembly, "Identity.Contracts", IdentityImplPrefixes),
        };
        foreach (var (asm, name, prefixes) in contracts)
        {
            // Contracts can reference their own .Domain transitively via BuildingBlocks' IIntegrationEvent,
            // but must never reference Application/Infrastructure/Api/Worker namespaces.
            var implOnly = prefixes.Where(p => !p.EndsWith(".Domain")).ToArray();
            AssertNoImplDependency(asm, name, asm.GetName().Name ?? "Contracts", implOnly);
        }
    }

    [Fact]
    public void Every_aggregate_root_must_implement_IMultiTenant_except_top_level_Tenant_aggregate()
    {
        var assemblies = new[] { CatalogApi, OrdersApi, IdentityApi, NotificationsApi, PlatformApi };
        var roots = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        InheritsFromGenericAggregateRoot(t))
            .ToList();

        var missing = roots
            .Where(t => !typeof(BuildingBlocks.Domain.IMultiTenant).IsAssignableFrom(t))
            // Exception: the Tenant aggregate IS the tenant, so it can't be tenant-scoped.
            .Where(t => t.FullName != "Identity.Domain.Tenants.Tenant")
            .Select(t => t.FullName!)
            .ToList();

        missing.Should().BeEmpty(
            $"every AggregateRoot<TId> must implement IMultiTenant (Tenant itself excepted). Missing: {string.Join(", ", missing)}");
    }

    private static bool InheritsFromGenericAggregateRoot(Type t)
    {
        var b = t.BaseType;
        while (b is not null)
        {
            if (b.IsGenericType && b.GetGenericTypeDefinition() == typeof(BuildingBlocks.Domain.AggregateRoot<>))
                return true;
            b = b.BaseType;
        }
        return false;
    }

    private static void AssertNoImplDependency(Assembly asm, string asmName, string other, string[] otherImplPrefixes)
    {
        var result = Types.InAssembly(asm)
            .Should().NotHaveDependencyOnAny(otherImplPrefixes)
            .GetResult();
        result.IsSuccessful.Should().BeTrue(
            $"{asmName} must not reach into {other} impl. Violating types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
