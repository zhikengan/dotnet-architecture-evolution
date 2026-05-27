using Marketplace.Domain.Common;
using MediatR;

namespace Marketplace.Application.Products.Queries.ListProductsForBuyer;

public sealed record ListProductsForBuyerQuery
    : IRequest<Result<IReadOnlyList<BuyerProductDto>>>;
