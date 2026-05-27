namespace BuildingBlocks.Application;

public interface IIdempotentCommand
{
    string? IdempotencyKey { get; init; }
}
