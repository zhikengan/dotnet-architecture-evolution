using Marketplace.Domain.Common;
using MediatR;

namespace Marketplace.Application.Orders.CancelOwnOrder;

public sealed record CancelOwnOrderCommand(Guid OrderId, Guid BuyerId) : IRequest<Result>;
