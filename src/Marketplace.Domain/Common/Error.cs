namespace Marketplace.Domain.Common;

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public override string ToString() => string.IsNullOrEmpty(Code) ? "<none>" : $"{Code}: {Message}";
}
