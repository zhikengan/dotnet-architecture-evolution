using BuildingBlocks.Application;

namespace BuildingBlocks.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
