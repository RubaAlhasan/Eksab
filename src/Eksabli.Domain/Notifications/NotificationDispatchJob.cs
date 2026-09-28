using System;
using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Notifications;

// First IBackgroundJobManager-driven job in this repo (the two existing background pieces —
// Wallets.PointsExpirationWorker, Billing.SubscriptionRenewalWorker — are periodic sweeps, not
// per-item queue jobs). CampaignSweepWorker enqueues one of these per notification instead of calling
// INotificationSender inline, so a slow/failing channel provider can't stall the sweep itself.
//
// ITransientDependency is load-bearing, not decorative: AsyncBackgroundJob<TArgs> (the ABP base class)
// implements no DI marker interface of its own (confirmed by decompiling
// Volo.Abp.BackgroundJobs.Abstractions), so without this the job NAME resolves fine (registered via
// Configure<AbpBackgroundJobOptions> in EksabliDomainModule) but the executer then fails with "The job
// type is not registered to DI: Eksabli.Notifications.NotificationDispatchJob" — confirmed live, this
// was the second of two independent registration gaps stacked on top of each other.
public class NotificationDispatchJob : AsyncBackgroundJob<NotificationDispatchArgs>, ITransientDependency
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
