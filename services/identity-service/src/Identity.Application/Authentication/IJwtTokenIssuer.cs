namespace Identity.Application.Authentication;

public interface IJwtTokenIssuer
{
    string Issue(Guid userId, string role, Guid tenantId);

    /// <summary>JWKS object (key set) for the public key used to sign tokens.</summary>
    object GetJwks();
}
