using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Notifications;

public interface INotificationRepository : IRepository<Notification, Guid>
{
    Task<(List<Notification> Items, int TotalCount)> GetListAsync(
        Guid? campaignId = null,
        NotificationStatus? status = null,
        NotificationChannel? channel = null,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default);

    // Sweep idempotency — a membership is only ever notified once per campaign.
    Task<bool> ExistsForCampaignAsync(Guid campaignId, Guid membershipId, CancellationToken cancellationToken = default);

    // Per-tenant fan-out quota — see NotificationConsts.MaxDailyNotificationsPerTenant.
    Task<int> CountCreatedSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default);
}
