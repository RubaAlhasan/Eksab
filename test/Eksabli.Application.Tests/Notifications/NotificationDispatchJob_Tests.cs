using System;
using System.Threading.Tasks;
using Eksabli.Memberships;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.Notifications;

// Regression coverage for a real gap: ABP's own background job system carries no ambient tenant at all
// (BackgroundJobInfo has no TenantId — a documented framework limitation, not something this app
// controls), but Notification.TenantId is the real business tenant. The standard IMultiTenant filter
// made FindAsync return null for every real notification the job was ever asked to dispatch — silently:
// returning without throwing is treated as SUCCESS by the job executer, so there was no exception, no
// log, and the job row just vanished from the queue while the Notification itself sat at Status=Queued
// forever. This test reproduces the actual execution context (no ambient tenant at all), not the
// convenience of an already-correct one, which is exactly what let this ship unnoticed.
public abstract class NotificationDispatchJob_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly NotificationDispatchJob _job;
    private readonly INotificationRepository _notificationRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guidGenerator;

    protected NotificationDispatchJob_Tests()
    {
        _job = GetRequiredService<NotificationDispatchJob>();
        _notificationRepository = GetRequiredService<INotificationRepository>();
        _membershipRepository = GetRequiredService<IRepository<Membership, Guid>>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
    }

    [Fact]
    public async Task ExecuteAsync_Should_Dispatch_A_Notification_With_No_Ambient_Tenant()
    {
        Guid tenantId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            var tenant = await _tenantManager.CreateAsync("tenant-" + Guid.NewGuid().ToString("N"));
            await _tenantRepository.InsertAsync(tenant, autoSave: true);
            tenantId = tenant.Id;
        });

        Guid notificationId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var membership = Membership.Create(_guidGenerator.Create(), Guid.NewGuid(), DateTime.UtcNow);
                await _membershipRepository.InsertAsync(membership, autoSave: true);

                // InApp is never plan-gated and always "succeeds" (NotificationSender's own comment: the
                // Notification row itself IS the in-app delivery) — isolates this test to the tenant-
                // filtering bug rather than any push/email/SMS provider behavior.
                var notification = Notification.Create(_guidGenerator.Create(), membership.Id, NotificationChannel.InApp, "Test", "Body");
                await _notificationRepository.InsertAsync(notification, autoSave: true);
                notificationId = notification.Id;
            }
        });

        // The real execution context: ABP's background job system carries no ambient tenant at all.
        using (_currentTenant.Change(null))
        {
            await WithUnitOfWorkAsync(() => _job.ExecuteAsync(new NotificationDispatchArgs { NotificationId = notificationId }));
        }

        using (_currentTenant.Change(tenantId))
        {
            var reloaded = await WithUnitOfWorkAsync(() => _notificationRepository.GetAsync(notificationId));
            reloaded.Status.ShouldBe(NotificationStatus.Sent);
            reloaded.SentAt.ShouldNotBeNull();
        }
    }
}
