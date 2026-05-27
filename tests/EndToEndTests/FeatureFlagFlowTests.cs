using System.Net.Http.Json;
using EndToEndTests.Fixtures;

namespace EndToEndTests;

[Collection(nameof(ApiCollection))]
public class FeatureFlagFlowTests(ApiFixture fx) : IAsyncLifetime
{
    public Task InitializeAsync() => fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Buyer_sees_isPremium_false_at_zero_percent_rollout()
    {
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var products = await buyer.GetFromJsonAsync<BuyerProduct[]>("/api/buyer/products");
        products!.Should().OnlyContain(p => p.IsPremium == false);
    }

    [Fact]
    public async Task Admin_sets_100_percent_rollout_buyer_eventually_sees_isPremium_true()
    {
        var admin = fx.ClientFor("Admin", ApiFixture.AdminId);
        var resp = await admin.PutAsJsonAsync("/api/admin/feature-flags/EnablePremiumBadge/rollout", new { percentage = 100 });
        resp.EnsureSuccessStatusCode();

        // Cache TTL is 1s in tests. Poll up to 3 seconds for the change to propagate.
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            var products = await buyer.GetFromJsonAsync<BuyerProduct[]>("/api/buyer/products");
            if (products!.All(p => p.IsPremium)) return;
            await Task.Delay(250);
        }
        var final = await buyer.GetFromJsonAsync<BuyerProduct[]>("/api/buyer/products");
        final!.Should().OnlyContain(p => p.IsPremium, "buyer should see premium after cache TTL elapses");
    }

    [Fact]
    public async Task Admin_can_enable_flag_for_specific_user()
    {
        var admin = fx.ClientFor("Admin", ApiFixture.AdminId);
        var resp = await admin.PutAsync($"/api/admin/feature-flags/EnablePremiumBadge/users/{ApiFixture.BuyerId}", content: null);
        resp.EnsureSuccessStatusCode();

        // Wait for cache to refresh
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            var products = await buyer.GetFromJsonAsync<BuyerProduct[]>("/api/buyer/products");
            if (products!.All(p => p.IsPremium)) return;
            await Task.Delay(250);
        }
        var final = await buyer.GetFromJsonAsync<BuyerProduct[]>("/api/buyer/products");
        final!.Should().OnlyContain(p => p.IsPremium);
    }

    private sealed record BuyerProduct(Guid Id, string Name, decimal Price, bool InStock, bool IsPremium);
}
