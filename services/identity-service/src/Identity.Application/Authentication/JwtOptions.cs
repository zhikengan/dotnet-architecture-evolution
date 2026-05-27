namespace Identity.Application.Authentication;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "marketplace-identity";
    public string Audience { get; set; } = "marketplace";
    public int LifetimeMinutes { get; set; } = 60;

    /// <summary>PEM-encoded RSA private key (PKCS#8). Used to sign tokens.</summary>
    public string PrivateKeyPem { get; set; } = string.Empty;

    /// <summary>PEM-encoded RSA public key (X.509 SubjectPublicKeyInfo). Used in JWKS.</summary>
    public string PublicKeyPem { get; set; } = string.Empty;

    public string KeyId { get; set; } = "identity-key-1";
}
