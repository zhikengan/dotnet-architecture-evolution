using BuildingBlocks.Application;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions;
using Platform.Domain.FeatureFlags.Errors;

namespace Platform.Application.FeatureFlags.ToggleFlag;

public sealed record ToggleFlagCommand(string Name) : IRequest<Result>, IAuthorizationRequirement
{
    public string[] AllowedRoles { get; } = ["Admin"];
}

public sealed class ToggleFlagHandler(IPlatformDbContext db, IClock clock) : IRequestHandler<ToggleFlagCommand, Result>
{
    public async Task<Result> Handle(ToggleFlagCommand cmd, CancellationToken ct)
    {
        var flag = await db.FeatureFlags.FirstOrDefaultAsync(f => f.Id == cmd.Name, ct);
        if (flag is null) return Result.Failure(FeatureFlagErrors.NotFound);

        flag.Toggle(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
