using System.ComponentModel.DataAnnotations;

namespace BuildingBlocks.Infrastructure.Authentication;

/// <summary>
/// Symmetric-key JWT configuration shared across the host and any future hosts
/// that share <c>BuildingBlocks</c>. HS256 is dev-grade; Tier 4 graduates to
/// RS256 + a real issuer.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required, MinLength(32)]
    public string Key { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = "marketplace";

    [Required]
    public string Audience { get; init; } = "marketplace-clients";

    public int LifetimeMinutes { get; init; } = 60;
}
