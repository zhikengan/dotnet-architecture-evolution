namespace BuildingBlocks.Domain;

public sealed class DomainException(Error error) : Exception(error.ToString())
{
    public Error Error { get; } = error;
}
