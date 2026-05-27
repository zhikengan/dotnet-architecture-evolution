namespace Identity.Domain.Users;

public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public enum UserRole
{
    Buyer = 0,
    Seller = 1,
    Admin = 2,
}
