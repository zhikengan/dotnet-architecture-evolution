namespace BuildingBlocks.Application;

public interface ICorrelationContext
{
    string CorrelationId { get; }
}

public sealed class CorrelationContext : ICorrelationContext
{
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");
}
