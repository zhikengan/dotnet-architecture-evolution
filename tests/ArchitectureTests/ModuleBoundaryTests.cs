using NetArchTest.Rules;

namespace ArchitectureTests;

public class ModuleBoundaryTests
{
    // Forbidden namespace prefixes that target ONLY a module's impl, not its Contracts.
    private static readonly string[] CatalogImplNamespaces =
    [
        "Catalog.Domain",
        "Catalog.Application",
        "Catalog.Infrastructure",
    ];

    private static readonly string[] OrdersImplNamespaces =
    [
        "Orders.Domain",
        "Orders.Application",
        "Orders.Infrastructure",
    ];

    private static readonly string[] PlatformImplNamespaces =
    [
        "Platform.Domain",
        "Platform.Application",
        "Platform.Infrastructure",
    ];

    private static readonly System.Reflection.Assembly CatalogAssembly = typeof(global::Catalog.CatalogModule).Assembly;
    private static readonly System.Reflection.Assembly OrdersAssembly = typeof(global::Orders.OrdersModule).Assembly;
    private static readonly System.Reflection.Assembly PlatformAssembly = typeof(global::Platform.PlatformModule).Assembly;

    [Fact]
    public void Catalog_must_not_reference_Orders_module_impl()
    {
        var result = Types.InAssembly(CatalogAssembly)
            .Should().NotHaveDependencyOnAny(OrdersImplNamespaces)
            .GetResult();
        AssertSuccess(result);
    }

    [Fact]
    public void Orders_must_not_reference_Catalog_module_impl()
    {
        var result = Types.InAssembly(OrdersAssembly)
            .Should().NotHaveDependencyOnAny(CatalogImplNamespaces)
            .GetResult();
        AssertSuccess(result);
    }

    [Fact]
    public void Catalog_must_not_reference_Platform_module_impl()
    {
        var result = Types.InAssembly(CatalogAssembly)
            .Should().NotHaveDependencyOnAny(PlatformImplNamespaces)
            .GetResult();
        AssertSuccess(result);
    }

    [Fact]
    public void Orders_must_not_reference_Platform_module_impl()
    {
        var result = Types.InAssembly(OrdersAssembly)
            .Should().NotHaveDependencyOnAny(PlatformImplNamespaces)
            .GetResult();
        AssertSuccess(result);
    }

    [Fact]
    public void Platform_must_not_reference_Catalog_or_Orders_impl()
    {
        var rCatalog = Types.InAssembly(PlatformAssembly).Should().NotHaveDependencyOnAny(CatalogImplNamespaces).GetResult();
        var rOrders = Types.InAssembly(PlatformAssembly).Should().NotHaveDependencyOnAny(OrdersImplNamespaces).GetResult();
        AssertSuccess(rCatalog);
        AssertSuccess(rOrders);
    }

    [Fact]
    public void Modules_must_not_reference_Api_host()
    {
        foreach (var asm in new[] { CatalogAssembly, OrdersAssembly, PlatformAssembly })
        {
            var result = Types.InAssembly(asm).Should().NotHaveDependencyOn("Marketplace.Api").GetResult();
            AssertSuccess(result);
        }
    }

    [Fact]
    public void Domain_namespaces_must_not_depend_on_EntityFrameworkCore()
    {
        foreach (var (asm, ns) in new[]
        {
            (CatalogAssembly, "Catalog.Domain"),
            (OrdersAssembly, "Orders.Domain"),
            (PlatformAssembly, "Platform.Domain"),
        })
        {
            var result = Types.InAssembly(asm)
                .That().ResideInNamespaceStartingWith(ns)
                .Should().NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
                .GetResult();
            AssertSuccess(result);
        }
    }

    [Fact]
    public void Domain_namespaces_must_not_depend_on_FluentValidation()
    {
        foreach (var (asm, ns) in new[]
        {
            (CatalogAssembly, "Catalog.Domain"),
            (OrdersAssembly, "Orders.Domain"),
            (PlatformAssembly, "Platform.Domain"),
        })
        {
            var result = Types.InAssembly(asm)
                .That().ResideInNamespaceStartingWith(ns)
                .Should().NotHaveDependencyOn("FluentValidation")
                .GetResult();
            AssertSuccess(result);
        }
    }

    private static void AssertSuccess(TestResult r)
    {
        var msg = r.FailingTypeNames is null || !r.FailingTypeNames.Any()
            ? "Architecture rule violated"
            : $"Violating types: {string.Join(", ", r.FailingTypeNames)}";
        r.IsSuccessful.Should().BeTrue(msg);
    }
}
