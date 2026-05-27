using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Infrastructure.Authentication;

/// <summary>
/// RS256 (asymmetric) JWT configuration. The private key signs tokens at the
/// API host's demo issuer; the public key is what any future relying party
/// (Worker host, external SDK) uses to validate without ever holding the
/// signing material. PEM-encoded RSA keys are read directly from config —
/// in dev they sit in <c>appsettings.Development.json</c>; in prod they
/// would come from a secret store and rotate via <c>KeyId</c>.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string PrivateKeyPem { get; init; } = string.Empty;

    [Required]
    public string PublicKeyPem { get; init; } = string.Empty;

    /// <summary>
    /// JWS <c>kid</c> header value. Surfaced via the JWKS discovery endpoint
    /// so clients can pin the rotation epoch.
    /// </summary>
    [Required]
    public string KeyId { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = "marketplace";

    [Required]
    public string Audience { get; init; } = "marketplace-clients";

    public int LifetimeMinutes { get; init; } = 60;
}
