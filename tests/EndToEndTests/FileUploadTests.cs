using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EndToEndTests.Fixtures;

namespace EndToEndTests;

/// <summary>
/// End-to-end of the presigned-URL upload flow against a real MinIO container.
/// Validates the same request sequence the seller's UI would issue:
/// 1) POST /image-upload-url → server returns a short-lived presigned PUT URL
/// 2) PUT bytes directly to MinIO using that URL (API host never sees the bytes)
/// 3) POST /image to confirm — server checks the object actually landed, then
///    persists the key on the Product so buyer DTOs surface an ImageUrl.
/// </summary>
[Collection(nameof(ApiCollection))]
public class FileUploadTests(ApiFixture fx) : IAsyncLifetime
{
    public Task InitializeAsync() => fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Full_upload_flow_lands_in_storage_and_surfaces_imageUrl()
    {
        var seller = fx.ClientFor("Seller", ApiFixture.SellerId);

        // 1) Request presigned URL
        var presignedResp = await seller.PostAsJsonAsync(
            $"/api/seller/products/{fx.WidgetId}/image-upload-url",
            new { contentType = "image/png" });
        presignedResp.EnsureSuccessStatusCode();
        var presigned = await presignedResp.Content.ReadFromJsonAsync<JsonElement>();
        var uploadUrl = presigned.GetProperty("uploadUrl").GetString();
        var key = presigned.GetProperty("key").GetString();
        uploadUrl.Should().NotBeNullOrEmpty();
        key.Should().NotBeNullOrEmpty();

        // 2) PUT bytes directly to MinIO via the presigned URL.
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]; // PNG magic
        using var rawClient = new HttpClient();
        var putContent = new ByteArrayContent(bytes);
        putContent.Headers.Add("Content-Type", "image/png");
        var putResp = await rawClient.PutAsync(uploadUrl, putContent);
        putResp.IsSuccessStatusCode.Should().BeTrue(
            $"presigned PUT must succeed; got {putResp.StatusCode}: {await putResp.Content.ReadAsStringAsync()}");

        // 3) Confirm the upload — handler verifies the object exists then sets the key.
        var confirmResp = await seller.PostAsJsonAsync(
            $"/api/seller/products/{fx.WidgetId}/image",
            new { key });
        confirmResp.EnsureSuccessStatusCode();
        var confirmed = await confirmResp.Content.ReadFromJsonAsync<JsonElement>();
        confirmed.GetProperty("imageUrl").GetString().Should().Contain(key!);

        // Buyer list now exposes the public URL.
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var products = await buyer.GetFromJsonAsync<BuyerProductRow[]>("/api/buyer/products");
        var widget = products!.Single(p => p.Id == fx.WidgetId);
        widget.ImageUrl.Should().NotBeNull().And.Contain(key!);
    }

    [Fact]
    public async Task Confirming_a_phantom_key_fails_with_400()
    {
        var seller = fx.ClientFor("Seller", ApiFixture.SellerId);
        var resp = await seller.PostAsJsonAsync(
            $"/api/seller/products/{fx.WidgetId}/image",
            new { key = "this-was-never-uploaded" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Buyer_token_cannot_request_upload_url()
    {
        var buyer = fx.ClientFor("Buyer", ApiFixture.BuyerId);
        var resp = await buyer.PostAsJsonAsync(
            $"/api/seller/products/{fx.WidgetId}/image-upload-url",
            new { contentType = "image/png" });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record BuyerProductRow(Guid Id, string Name, decimal Price, bool InStock, bool IsPremium, string? ImageUrl);
}
