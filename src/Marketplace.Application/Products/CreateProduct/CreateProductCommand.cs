using Marketplace.Domain.Common;
using MediatR;

namespace Marketplace.Application.Products.CreateProduct;

public sealed record CreateProductCommand(string Name, decimal Price, int Stock, Guid SellerId)
    : IRequest<Result<CreateProductResult>>;
