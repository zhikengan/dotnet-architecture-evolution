using BuildingBlocks.Application;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions;

namespace Platform.Application.FeatureFlags.Commands;

public sealed record SetRolloutPercentageCommand(Guid TenantId, string Key, int Percentage)
    : IRequest<Result>, IAuthorizationRequirement
{
    public string[] AllowedRoles { get; } = ["Admin"];
}

public sealed class SetRolloutPercentageValidator : AbstractValidator<SetRolloutPercentageCommand>
{
    public SetRolloutPercentageValidator()
    {
        RuleFor(x => x.TenantId).NotEqual(Guid.Empty);
        RuleFor(x => x.Key).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Percentage).InclusiveBetween(0, 100);
    }
}

public sealed class SetRolloutPercentageHandler(IPlatformDbContext db, IClock clock)
    : IRequestHandler<SetRolloutPercentageCommand, Result>
{
    public async Task<Result> Handle(SetRolloutPercentageCommand cmd, CancellationToken ct)
    {
        var flag = await db.FeatureFlags
            .FirstOrDefaultAsync(f => f.TenantId == cmd.TenantId && f.Key == cmd.Key, ct);
        if (flag is null) return Result.Failure(new Error("FeatureFlag.NotFound", "Feature flag not found"));

        var r = flag.SetRolloutPercentage(cmd.Percentage, clock.UtcNow);
        if (r.IsFailure) return r;

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
