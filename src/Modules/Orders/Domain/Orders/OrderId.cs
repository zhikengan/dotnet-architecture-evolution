namespace Orders.Domain.Orders;

public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Cancelled = 2,
    Failed = 3,
}
