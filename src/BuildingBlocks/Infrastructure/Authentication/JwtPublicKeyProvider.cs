using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Infrastructure.Authentication;

/// <summary>
/// Materializes the configured RSA public key once. The same instance backs
/// both JwtBearer validation and the JWKS discovery endpoint, so relying
/// parties and the validator always see identical key material + KeyId.
/// RSA is intentionally not disposed — see <see cref="JwtTokenIssuer"/>
/// for the parallel-test rationale.
/// </summary>
public sealed class JwtPublicKeyProvider
{
    private readonly RSA _publicKey;

    public JwtPublicKeyProvider(IOptions<JwtOptions> options)
    {
        var opts = options.Value;
        _publicKey = RSA.Create();
        _publicKey.ImportFromPem(opts.PublicKeyPem);
        SecurityKey = new RsaSecurityKey(_publicKey) { KeyId = opts.KeyId };
    }

    public RsaSecurityKey SecurityKey { get; }

    public JsonWebKey ToJwk()
    {
        var p = _publicKey.ExportParameters(includePrivateParameters: false);
        return new JsonWebKey
        {
            Kty = "RSA",
            Use = "sig",
            Alg = SecurityAlgorithms.RsaSha256,
            Kid = SecurityKey.KeyId,
            N = Base64UrlEncoder.Encode(p.Modulus!),
            E = Base64UrlEncoder.Encode(p.Exponent!),
        };
    }
}
