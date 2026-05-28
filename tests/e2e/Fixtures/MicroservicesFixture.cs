using System.Net.Http.Json;

namespace E2E.Fixtures;

/// <summary>
/// Probes the running Tier 5 stack. We don't spin services up here — the
/// e2e GitHub Actions workflow does <c>docker compose up -d --build --wait</c>
/// first; locally the developer runs the same. If nothing is reachable on
/// the buyer-BFF port the fixture flips <see cref="StackIsUp"/> to false and
/// every test in the suite skips with a clear message rather than failing.
/// </summary>
public sealed class MicroservicesFixture : IAsyncLifetime
{
    // The compose stack publishes:
    //   buyer-bff  :5010    seller-bff :5020    admin-bff  :5030
    //   identity   :5300    notifications :5400 (direct)
    public const string BuyerBffBase = "http://localhost:5010";
    public const string SellerBffBase = "http://localhost:5020";
    public const string AdminBffBase = "http://localhost:5030";
    public const string IdentityBase = "http://localhost:5300";
    public const string NotificationsBase = "http://localhost:5400";

    public static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid BuyerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public bool StackIsUp { get; private set; }
    public string? SkipReason { get; private set; }

    private readonly HttpClient _probe = new() { Timeout = TimeSpan.FromSeconds(2) };

    public async Task InitializeAsync()
    {
        try
        {
            var r = await _probe.GetAsync($"{IdentityBase}/.well-known/jwks.json");
            StackIsUp = r.IsSuccessStatusCode;
            if (!StackIsUp) SkipReason = $"identity-service unreachable at {IdentityBase} (HTTP {(int)r.StatusCode})";
        }
        catch (Exception ex)
        {
            StackIsUp = false;
            SkipReason = $"compose stack not running ({ex.GetType().Name}: {ex.Message}). Run `cd deploy && docker compose up -d --build --wait` first.";
        }
    }

    public Task DisposeAsync()
    {
        _probe.Dispose();
        return Task.CompletedTask;
    }

    public HttpClient ClientFor(string baseUrl) => new() { BaseAddress = new Uri(baseUrl) };

    public async Task<string> MintTokenAsync(Guid userId, string role)
    {
        using var client = ClientFor(IdentityBase);
        var resp = await client.GetFromJsonAsync<TokenResponse>($"/demo/token?role={role}&userId={userId}");
        return resp!.Token;
    }

    public HttpClient AuthedClient(string baseUrl, string token)
    {
        var c = ClientFor(baseUrl);
        c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    /// <summary>Polls /api/buyer/orders/{id} via buyer-BFF until status == expected or timeout.</summary>
    public async Task<string?> WaitForOrderStatus(HttpClient buyer, Guid orderId, string expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        string? last = null;
        while (DateTime.UtcNow < deadline)
        {
            var resp = await buyer.GetAsync($"/orders/{orderId}");
            if (resp.IsSuccessStatusCode)
            {
                var dto = await resp.Content.ReadFromJsonAsync<OrderProbe>();
                last = dto?.Status;
                if (dto?.Status == expected) return expected;
            }
            await Task.Delay(250);
        }
        return last;
    }

    public sealed record TokenResponse(string Token, Guid UserId, Guid TenantId, string Role);
    public sealed record OrderProbe(Guid OrderId, Guid BuyerId, Guid ProductId, int Quantity, string Status);
}

[CollectionDefinition(nameof(MicroservicesCollection))]
public class MicroservicesCollection : ICollectionFixture<MicroservicesFixture>;
