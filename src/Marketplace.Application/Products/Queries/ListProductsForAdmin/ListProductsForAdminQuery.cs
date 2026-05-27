using Marketplace.Domain.Common;
using MediatR;

namespace Marketplace.Application.Products.Queries.ListProductsForAdmin;

public sealed record ListProductsForAdminQuery
    : IRequest<Result<IReadOnlyList<AdminProductDto>>>;
