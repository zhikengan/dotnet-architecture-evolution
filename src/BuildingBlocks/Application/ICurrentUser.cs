namespace BuildingBlocks.Application;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    string Role { get; }

    /// <summary>
    /// Tenant the authenticated principal is acting as, sourced from the
    /// <c>tenant_id</c> JWT claim. <see cref="Guid.Empty"/> when unauthenticated.
    /// </summary>
    Guid TenantId { get; }
}
