using BuildingBlocks.Domain;

namespace Identity.Domain.Users.Errors;

public static class UserErrors
{
    public static readonly Error InvalidEmail = new("User.InvalidEmail", "Email is required");
    public static readonly Error InvalidRole = new("User.InvalidRole", "Role must be Buyer, Seller, or Admin");
    public static readonly Error InvalidTenant = new("User.InvalidTenant", "TenantId is required");
    public static readonly Error NotFound = new("User.NotFound", "User not found");
}
