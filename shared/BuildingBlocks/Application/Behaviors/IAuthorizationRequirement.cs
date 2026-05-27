namespace BuildingBlocks.Application.Behaviors;

public interface IAuthorizationRequirement
{
    string[] AllowedRoles { get; }
}
