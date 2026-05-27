namespace Marketplace.Domain.Common;

public sealed class DomainException : Exception
{
    public Error Error { get; }

    public DomainException(Error error) : base(error.Message) => Error = error;
}
