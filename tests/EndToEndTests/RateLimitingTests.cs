using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BuildingBlocks.Infrastructure.Authentication;
using EndToEndTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EndToEndTests;

/// <summary>
/// The shared <see cref="ApiFixture"/> bumps limits to 10000/min so other
/// e2e tests don't accidentally trigger 429s. This test stands up its own
/// <see cref="WebApplicationFactory{TEntryPoint}"/> with very low limits
/// (3 writes/min) so we can verify the rejection behavior — both the 429
/// status and the <c>Retry-After</c> header. Requests past the threshold
/// short-circuit at the rate limiter before reaching the handler, so the
/// fact that no real database is wired doesn't matter for the rejection
/// path; we treat handler errors as "request was let through" while
/// hunting for the 429.
/// </summary>
public class RateLimitingTests
{
    [Fact]
    public async Task Exceeding_write_limit_returns_429_with_Retry_After()
    {
        await using var factory = new RateLimitedApiFactory();
        _ = factory.Services;

        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<JwtTokenIssuer>();
        var (token, _) = issuer.Mint(ApiFixture.SellerId, "Seller", ApiFixture.AcmeTenantId);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpStatusCode? sawTooMany = null;
        string? retryAfter = null;
        for (var i = 0; i < 20; i++)
        {
            HttpResponseMessage? resp = null;
            try
            {
                resp = await client.PostAsJsonAsync("/api/seller/products", new { name = $"P{i}", price = 1m, stock = 1 });
            }
            catch
            {
                // Handler crashed AFTER rate limiter allowed the request — token
                // was consumed; keep hunting for the eventual 429.
                continue;
            }

            if (resp.StatusCode == HttpStatusCode.TooManyRequests)
            {
                sawTooMany = resp.StatusCode;
                retryAfter = resp.Headers.TryGetValues("Retry-After", out var values) ? values.FirstOrDefault() : null;
                break;
            }
        }

        sawTooMany.Should().Be(HttpStatusCode.TooManyRequests);
        retryAfter.Should().NotBeNullOrEmpty("the rate-limiter must surface a Retry-After hint");
    }

    private sealed class RateLimitedApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // "Testing" skips Program.cs's IsDevelopment migrate-and-seed block.
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Marketplace"] = "Host=localhost;Database=ratelimit_unused;Username=x;Password=x",
                    ["Jwt:Issuer"] = "marketplace",
                    ["Jwt:Audience"] = "marketplace-clients",
                    ["Jwt:LifetimeMinutes"] = "60",
                    ["Jwt:KeyId"] = "rate-limit-tests",
                    ["Jwt:PrivateKeyPem"] = TestKeys.PrivateKeyPem,
                    ["Jwt:PublicKeyPem"] = TestKeys.PublicKeyPem,
                    ["RateLimit:Writes"] = "3",
                    ["RateLimit:Reads"] = "3",
                });
            });

            // Short-circuit the EF retry strategy so handler failures return
            // immediately (no 30-second Polly backoff). Requests rejected by
            // the rate limiter never reach the handler so the connection
            // string doesn't matter, but we still need handler invocations
            // to fail fast for the loop above to make progress.
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<MediatR.IPipelineBehavior<
                    Catalog.Application.Products.CreateProduct.CreateProductCommand,
                    BuildingBlocks.Domain.Result<Catalog.Application.Products.CreateProduct.CreateProductResult>>, FastFailHandler>();
            });
        }
    }

    /// <summary>
    /// Pipeline behavior that fails the CreateProduct request synchronously
    /// without ever invoking the EF handler — keeps the rate-limit test fast
    /// even though we never provisioned a database.
    /// </summary>
    private sealed class FastFailHandler : MediatR.IPipelineBehavior<
        Catalog.Application.Products.CreateProduct.CreateProductCommand,
        BuildingBlocks.Domain.Result<Catalog.Application.Products.CreateProduct.CreateProductResult>>
    {
        public Task<BuildingBlocks.Domain.Result<Catalog.Application.Products.CreateProduct.CreateProductResult>> Handle(
            Catalog.Application.Products.CreateProduct.CreateProductCommand request,
            MediatR.RequestHandlerDelegate<BuildingBlocks.Domain.Result<Catalog.Application.Products.CreateProduct.CreateProductResult>> next,
            CancellationToken ct) =>
            Task.FromResult(BuildingBlocks.Domain.Result.Failure<Catalog.Application.Products.CreateProduct.CreateProductResult>(
                new BuildingBlocks.Domain.Error("Test.NoOp", "Test stub")));
    }
}
