using BuildingBlocks.Application;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Domain;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions;
using Platform.Contracts.IntegrationEvents;

namespace Platform.Application.FeatureFlags.Commands;

public sealed record ToggleFlagCommand(Guid TenantId, string Key, bool IsEnabled)
    : IRequest<Result>, IAuthorizationRequirement
{
    public string[] AllowedRoles { get; } = ["Admin"];
}

public sealed class ToggleFlagValidator : AbstractValidator<ToggleFlagCommand>
{
    public ToggleFlagValidator()
    {
        RuleFor(x => x.TenantId).NotEqual(Guid.Empty);
        RuleFor(x => x.Key).NotEmpty().MaximumLength(120);
    }
}

public sealed class ToggleFlagHandler(IPlatformDbContext db, IClock clock, IPublishEndpoint bus)
    : IRequestHandler<ToggleFlagCommand, Result>
{
    public async Task<Result> Handle(ToggleFlagCommand cmd, CancellationToken ct)
    {
        var flag = await db.FeatureFlags
            .FirstOrDefaultAsync(f => f.TenantId == cmd.TenantId && f.Key == cmd.Key, ct);
        if (flag is null) return Result.Failure(new Error("FeatureFlag.NotFound", "Feature flag not found"));

        flag.Toggle(cmd.IsEnabled, clock.UtcNow);

        await bus.Publish(new FeatureFlagToggledIntegrationEvent(
            MessageId: Guid.NewGuid(),
            OccurredAt: clock.UtcNow,
            TenantId: flag.TenantId,
            Key: flag.Key,
            IsEnabled: flag.IsEnabled), ct);

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
