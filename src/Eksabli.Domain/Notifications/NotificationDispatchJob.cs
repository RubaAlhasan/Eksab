using System;
using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

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
//
// A THIRD, separate gap on top of those two, also confirmed live: this runs as a background job with no
// ambient tenant at all (ABP's own BackgroundJobInfo carries no tenant — this is a documented framework
// limitation, not something this app controls), but Notification.TenantId is the real business tenant.
// The standard IMultiTenant filter silently made FindAsync return null for every real notification, and
// returning without throwing is treated as SUCCESS by the job executer — no error, no log, the job row
// just vanishes from the queue, and the Notification itself sits at Status=Queued forever since the
// actual send/MarkSent/MarkFailed logic never ran. Same root cause as UserNotification's own filtering
// bug, just surfacing here as "nothing happens" instead of "wrong count vs. empty list."
public class NotificationDispatchJob : AsyncBackgroundJob<NotificationDispatchArgs>, ITransientDependency
{
    private readonly IRepository<Notification, Guid> _notificationRepository;
    private readonly INotificationSender _sender;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentTenant _currentTenant;

    public NotificationDispatchJob(
        IRepository<Notification, Guid> notificationRepository,
        INotificationSender sender,
        IDataFilter dataFilter,
        ICurrentTenant currentTenant)
    {
        _notificationRepository = notificationRepository;
        _sender = sender;
        _dataFilter = dataFilter;
        _currentTenant = currentTenant;
    }

    public override async Task ExecuteAsync(NotificationDispatchArgs args)
    {
        Notification? notification;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            notification = await _notificationRepository.FindAsync(args.NotificationId);
        }

        if (notification == null)
        {
            return;
        }

        // From here on, operate as if this were a normal request for the notification's own business —
        // NotificationSender.SendAsync looks up a tenant-scoped Membership internally, and its own
        // Device/IdentityUser lookups already assume a correct ambient business tenant to switch away
        // from (see that class's own _currentTenant.Change(null) calls).
        using (_currentTenant.Change(notification.TenantId))
        {
            await _sender.SendAsync(notification);
            await _notificationRepository.UpdateAsync(notification);
        }
    }
}
