using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Notifications;

public interface IUserNotificationRepository : IRepository<UserNotification, Guid>
{
    Task<(List<UserNotificationFeedItem> Items, long TotalCount)> GetFeedAsync(
        Guid userId,
        bool? isRead = null,
        int skipCount = 0,
        int maxResultCount = 20,
        CancellationToken cancellationToken = default);

    Task<long> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(Guid userId, DateTime readAt, CancellationToken cancellationToken = default);
}
