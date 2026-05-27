using Microsoft.EntityFrameworkCore;
using Notifications.Domain.Notifications;

namespace Notifications.Application.Abstractions;

public interface INotificationsDbContext
{
    DbSet<Notification> Notifications { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
