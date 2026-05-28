using BuildingBlocks.Application;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions;

namespace Platform.Application.FeatureFlags.Commands;

public sealed record EnableForUserCommand(Guid TenantId, string Key, Guid UserId)
    : IRequest<Result>, IAuthorizationRequirement
{
    public string[] AllowedRoles { get; } = ["Admin"];
}

public sealed class EnableForUserValidator : AbstractValidator<EnableForUserCommand>
{
    public EnableForUserValidator()
    {
        RuleFor(x => x.TenantId).NotEqual(Guid.Empty);
        RuleFor(x => x.Key).NotEmpty().MaximumLength(120);
        RuleFor(x => x.UserId).NotEqual(Guid.Empty);
    }
}

public sealed class EnableForUserHandler(IPlatformDbContext db, IClock clock)
    : IRequestHandler<EnableForUserCommand, Result>
{
    public async Task<Result> Handle(EnableForUserCommand cmd, CancellationToken ct)
    {
        var flag = await db.FeatureFlags
            .FirstOrDefaultAsync(f => f.TenantId == cmd.TenantId && f.Key == cmd.Key, ct);
        if (flag is null) return Result.Failure(new Error("FeatureFlag.NotFound", "Feature flag not found"));

        flag.EnableForUser(cmd.UserId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
