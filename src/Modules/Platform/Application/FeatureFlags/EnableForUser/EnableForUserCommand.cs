using BuildingBlocks.Application;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions;
using Platform.Domain.FeatureFlags.Errors;

namespace Platform.Application.FeatureFlags.EnableForUser;

public sealed record EnableForUserCommand(string Name, Guid UserId) : IRequest<Result>, IAuthorizationRequirement
{
    public string[] AllowedRoles { get; } = ["Admin"];
}

public sealed class EnableForUserHandler(IPlatformDbContext db, IClock clock) : IRequestHandler<EnableForUserCommand, Result>
{
    public async Task<Result> Handle(EnableForUserCommand cmd, CancellationToken ct)
    {
        var flag = await db.FeatureFlags.FirstOrDefaultAsync(f => f.Id == cmd.Name, ct);
        if (flag is null) return Result.Failure(FeatureFlagErrors.NotFound);

        flag.EnableForUser(cmd.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
