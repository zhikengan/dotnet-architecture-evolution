using Marketplace.Application.Abstractions;

namespace Marketplace.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
