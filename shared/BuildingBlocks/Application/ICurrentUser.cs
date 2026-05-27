namespace BuildingBlocks.Application;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    string Role { get; }
    Guid TenantId { get; }
}
