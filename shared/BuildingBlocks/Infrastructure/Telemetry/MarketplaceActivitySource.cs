using System.Diagnostics;

namespace BuildingBlocks.Infrastructure.Telemetry;

public static class MarketplaceActivitySource
{
    public const string Name = "Marketplace";
    public static readonly ActivitySource Instance = new(Name, "1.0.0");
}
