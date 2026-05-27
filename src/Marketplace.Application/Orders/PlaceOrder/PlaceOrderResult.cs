namespace Marketplace.Application.Orders.PlaceOrder;

public sealed record PlaceOrderResult(Guid OrderId, string Status);
