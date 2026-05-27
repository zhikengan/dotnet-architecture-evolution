using Marketplace.Domain.Common;
using MediatR;

namespace Marketplace.Application.Orders.PlaceOrder;

public sealed record PlaceOrderCommand(Guid BuyerId, Guid ProductId, int Quantity)
    : IRequest<Result<PlaceOrderResult>>;
