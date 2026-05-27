using Marketplace.Domain.Common;
using MediatR;

namespace Marketplace.Application.Orders.ForceCancelOrder;

public sealed record ForceCancelOrderCommand(Guid OrderId) : IRequest<Result>;
