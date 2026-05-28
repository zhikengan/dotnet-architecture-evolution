using Identity.Domain.Tenants;
using Identity.Domain.Tenants.Errors;
using Identity.Domain.Tenants.Events;
using Identity.Domain.Users;
using Identity.Domain.Users.Errors;
using Identity.Domain.Users.Events;

namespace Identity.UnitTests;

public class UserTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Tenant = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void Create_valid_user_raises_UserCreated()
    {
        var r = User.Create("user@example.com", UserRole.Buyer, Tenant, Now);
        r.IsSuccess.Should().BeTrue();
        r.Value.Role.Should().Be(UserRole.Buyer);
        r.Value.DomainEvents.Should().ContainSingle(e => e is UserCreated);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_with_blank_email_fails(string email) =>
        User.Create(email, UserRole.Buyer, Tenant, Now).Error.Should().Be(UserErrors.InvalidEmail);

    [Fact]
    public void Create_with_empty_tenant_fails() =>
        User.Create("u@example.com", UserRole.Buyer, Guid.Empty, Now).Error.Should().Be(UserErrors.InvalidTenant);
}

public class TenantTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_valid_tenant_raises_TenantCreated()
    {
        var r = Tenant.Create("acme", Now);
        r.IsSuccess.Should().BeTrue();
        r.Value.Name.Should().Be("acme");
        r.Value.DomainEvents.Should().ContainSingle(e => e is TenantCreated);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_with_blank_name_fails(string name) =>
        Tenant.Create(name, Now).Error.Should().Be(TenantErrors.InvalidName);

    [Fact]
    public void Create_with_too_long_name_fails() =>
        Tenant.Create(new string('x', 101), Now).Error.Should().Be(TenantErrors.InvalidName);
}
