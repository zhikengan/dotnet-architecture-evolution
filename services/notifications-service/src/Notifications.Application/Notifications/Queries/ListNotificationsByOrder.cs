using BuildingBlocks.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Notifications.Application.Abstractions;

namespace Notifications.Application.Notifications.Queries;

public sealed record NotificationDto(Guid Id, Guid TenantId, string Type, string Recipient, string Body, DateTime SentAt);

public sealed record ListNotificationsByOrderQuery(Guid OrderId) : IRequest<Result<IReadOnlyList<NotificationDto>>>;

public sealed class ListNotificationsByOrderHandler(INotificationsDbContext db)
    : IRequestHandler<ListNotificationsByOrderQuery, Result<IReadOnlyList<NotificationDto>>>
{
    public async Task<Result<IReadOnlyList<NotificationDto>>> Handle(ListNotificationsByOrderQuery q, CancellationToken ct)
    {
        var rows = await db.Notifications.AsNoTracking()
            .Where(n => n.RelatedOrderId == q.OrderId)
            .OrderByDescending(n => n.SentAt)
            .ToListAsync(ct);
        IReadOnlyList<NotificationDto> dtos = rows
            .Select(n => new NotificationDto(n.Id.Value, n.TenantId, n.Type, n.Recipient, n.Body, n.SentAt))
            .ToList();
        return Result.Success(dtos);
    }
}
