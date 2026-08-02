using System;
using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Notifications;

// First IBackgroundJobManager-driven job in this repo (the two existing background pieces —
// Wallets.PointsExpirationWorker, Billing.SubscriptionRenewalWorker — are periodic sweeps, not
// per-item queue jobs). CampaignSweepWorker enqueues one of these per notification instead of calling
// INotificationSender inline, so a slow/failing channel provider can't stall the sweep itself.
public class NotificationDispatchJob : AsyncBackgroundJob<NotificationDispatchArgs>
{
    private readonly IRepository<Notification, Guid> _notificationRepository;
    private readonly INotificationSender _sender;

    public NotificationDispatchJob(IRepository<Notification, Guid> notificationRepository, INotificationSender sender)
    {
        _notificationRepository = notificationRepository;
        _sender = sender;
    }

    public override async Task ExecuteAsync(NotificationDispatchArgs args)
    {
        var notification = await _notificationRepository.FindAsync(args.NotificationId);
        if (notification == null)
        {
            return;
        }

        await _sender.SendAsync(notification);
        await _notificationRepository.UpdateAsync(notification);
    }
}
