using BuildingBlocks.Domain;
using Identity.Domain.Users.Errors;
using Identity.Domain.Users.Events;

namespace Identity.Domain.Users;

public sealed class User : AggregateRoot<UserId>, IMultiTenant
{
    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private User() { }

    public static Result<User> Create(string email, UserRole role, Guid tenantId, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(email)) return Result.Failure<User>(UserErrors.InvalidEmail);
        if (tenantId == Guid.Empty) return Result.Failure<User>(UserErrors.InvalidTenant);

        var user = new User
        {
            Id = UserId.New(),
            TenantId = tenantId,
            Email = email,
            Role = role,
            CreatedAt = now,
        };
        user.RaiseDomainEvent(new UserCreated(user.Id, tenantId, email, role));
        return Result.Success(user);
    }

    public static User Seed(Guid id, Guid tenantId, string email, UserRole role, DateTime now) => new()
    {
        Id = new UserId(id),
        TenantId = tenantId,
        Email = email,
        Role = role,
        CreatedAt = now,
    };
}
