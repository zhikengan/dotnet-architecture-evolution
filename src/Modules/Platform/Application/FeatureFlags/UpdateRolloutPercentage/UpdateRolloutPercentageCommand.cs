using BuildingBlocks.Application;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions;
using Platform.Domain.FeatureFlags.Errors;

namespace Platform.Application.FeatureFlags.UpdateRolloutPercentage;

public sealed record UpdateRolloutPercentageCommand(string Name, int Percentage) : IRequest<Result>, IAuthorizationRequirement
{
    public string[] AllowedRoles { get; } = ["Admin"];
}

public sealed class UpdateRolloutPercentageValidator : AbstractValidator<UpdateRolloutPercentageCommand>
{
    public UpdateRolloutPercentageValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Percentage).InclusiveBetween(0, 100);
    }
}

public sealed class UpdateRolloutPercentageHandler(IPlatformDbContext db, IClock clock) : IRequestHandler<UpdateRolloutPercentageCommand, Result>
{
    public async Task<Result> Handle(UpdateRolloutPercentageCommand cmd, CancellationToken ct)
    {
        var flag = await db.FeatureFlags.FirstOrDefaultAsync(f => f.Id == cmd.Name, ct);
        if (flag is null) return Result.Failure(FeatureFlagErrors.NotFound);

        var r = flag.SetRolloutPercentage(cmd.Percentage, clock.UtcNow);
        if (r.IsFailure) return r;

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
