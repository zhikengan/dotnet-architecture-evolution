using System.ComponentModel.DataAnnotations;

namespace Marketplace.Api.Configuration;

public sealed class AppOptions
{
    public const string SectionName = "App";

    [Required, MinLength(1)]
    public string Name { get; init; } = "Marketplace";

    public bool SeedOnStartup { get; init; } = true;
}
