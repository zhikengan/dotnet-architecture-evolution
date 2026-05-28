using NetArchTest.Rules;

namespace Catalog.UnitTests;

/// <summary>
/// Per-service architectural facts. Repo-level cross-service rules live in
/// <c>tests/architecture/</c>; these enforce per-layer purity inside catalog.
/// </summary>
public class ArchitectureTests
{
    private static readonly System.Reflection.Assembly DomainAsm = typeof(global::Catalog.Domain.Products.Product).Assembly;
    private static readonly System.Reflection.Assembly AppAsm = typeof(global::Catalog.Application.Products.CreateProduct.CreateProductCommand).Assembly;

    [Fact]
    public void Domain_must_not_depend_on_EntityFrameworkCore()
    {
        var result = Types.InAssembly(DomainAsm)
            .Should().NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();
        result.IsSuccessful.Should().BeTrue($"violating types: {Names(result.FailingTypeNames)}");
    }

    [Fact]
    public void Domain_must_not_depend_on_MassTransit()
    {
        var result = Types.InAssembly(DomainAsm)
            .Should().NotHaveDependencyOn("MassTransit")
            .GetResult();
        result.IsSuccessful.Should().BeTrue($"violating types: {Names(result.FailingTypeNames)}");
    }

    [Fact]
    public void Application_must_not_depend_on_Microsoft_AspNetCore()
    {
        var result = Types.InAssembly(AppAsm)
            .Should().NotHaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();
        result.IsSuccessful.Should().BeTrue($"violating types: {Names(result.FailingTypeNames)}");
    }

    private static string Names(IEnumerable<string>? names) =>
        names is null ? string.Empty : string.Join(", ", names);
}
