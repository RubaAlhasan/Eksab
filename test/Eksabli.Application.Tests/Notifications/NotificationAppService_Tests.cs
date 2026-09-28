using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Eksabli.Memberships;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.Notifications;

// Regression coverage for a real gap: the Business Portal's "Compose" send (SendAsync) only ever wrote
// a Push/Email/Sms/InApp *delivery-attempt* record (Notification), never the actual UserNotification
// row /customer/alerts reads — a staff-composed message never reached the customer regardless of which
// channel was picked, or whether NotificationDispatchJob ever successfully dispatched it.
public abstract class NotificationAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly INotificationAppService _notificationAppService;
    private readonly IUserNotificationAppService _userNotificationAppService;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IGuidGenerator _guidGenerator;

    protected NotificationAppService_Tests()
    {
        _notificationAppService = GetRequiredService<INotificationAppService>();
        _userNotificationAppService = GetRequiredService<IUserNotificationAppService>();
        _membershipRepository = GetRequiredService<IRepository<Membership, Guid>>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
    }

    private IDisposable LoginAs(Guid userId)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(AbpClaimTypes.UserId, userId.ToString()));
        return _currentPrincipalAccessor.Change(new ClaimsPrincipal(identity));
    }

    [Fact]
    public async Task SendAsync_Should_Deliver_To_The_Members_InApp_Inbox()
    {
        Guid tenantId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            var tenant = await _tenantManager.CreateAsync("tenant-" + Guid.NewGuid().ToString("N"));
            await _tenantRepository.InsertAsync(tenant, autoSave: true);
            tenantId = tenant.Id;
        });

        var customerId = Guid.NewGuid();
        Guid membershipId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var membership = Membership.Create(_guidGenerator.Create(), customerId, DateTime.UtcNow);
                await _membershipRepository.InsertAsync(membership, autoSave: true);
                membershipId = membership.Id;
            }
        });

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                await _notificationAppService.SendAsync(new SendNotificationDto
                {
                    MembershipId = membershipId,
                    Channel = NotificationChannel.Push,
                    Title = "Special Offer",
                    Body = "Just for you!"
                });
            }
        });

        using (_currentTenant.Change(tenantId))
        using (LoginAs(customerId))
        {
            (await WithUnitOfWorkAsync(() => _userNotificationAppService.GetUnreadCountAsync())).ShouldBe(1);

            var list = await WithUnitOfWorkAsync(() => _userNotificationAppService.GetListAsync(new UserNotificationListFilterDto()));
            list.Items[0].Title.ShouldBe("Special Offer");
            list.Items[0].Category.ShouldBe("Manual");
        }
    }
}
