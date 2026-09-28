using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.Notifications;

public abstract class UserNotificationAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IUserNotificationAppService _userNotificationAppService;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IdentityUserManager _identityUserManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    protected UserNotificationAppService_Tests()
    {
        _userNotificationAppService = GetRequiredService<IUserNotificationAppService>();
        _notificationPublisher = GetRequiredService<INotificationPublisher>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _identityUserManager = GetRequiredService<IdentityUserManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private IDisposable LoginAs(Guid userId)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(AbpClaimTypes.UserId, userId.ToString()));
        return _currentPrincipalAccessor.Change(new ClaimsPrincipal(identity));
    }

    private async Task<Guid> CreateTenantAsync()
    {
        Guid tenantId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            var tenant = await _tenantManager.CreateAsync("tenant-" + Guid.NewGuid().ToString("N"));
            await _tenantRepository.InsertAsync(tenant, autoSave: true);
            tenantId = tenant.Id;
        });
        return tenantId;
    }

    private async Task<Guid> CreateUserAsync(Guid? tenantId)
    {
        Guid userId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var email = $"user-{Guid.NewGuid():N}@example.com";
                var user = new IdentityUser(Guid.NewGuid(), email, email, tenantId);
                (await _identityUserManager.CreateAsync(user)).CheckErrors();
                userId = user.Id;
            }
        });
        return userId;
    }

    [Fact]
    public async Task Should_Deliver_A_Direct_Notification_And_Let_Its_Owner_Read_It()
    {
        var tenantId = await CreateTenantAsync();
        var userId = await CreateUserAsync(tenantId);

        await WithUnitOfWorkAsync(() => _notificationPublisher.PublishToUserAsync(
            userId, tenantId, UserNotificationType.Info, "Welcome", "Thanks for joining.", "test.welcome"));

        using (_currentTenant.Change(tenantId))
        using (LoginAs(userId))
        {
            (await WithUnitOfWorkAsync(() => _userNotificationAppService.GetUnreadCountAsync())).ShouldBe(1);

            var list = await WithUnitOfWorkAsync(() => _userNotificationAppService.GetListAsync(new UserNotificationListFilterDto()));
            list.TotalCount.ShouldBe(1);
            list.Items[0].Title.ShouldBe("Welcome");
            list.Items[0].Category.ShouldBe("test.welcome");
            list.Items[0].IsRead.ShouldBeFalse();

            await WithUnitOfWorkAsync(() => _userNotificationAppService.MarkAsReadAsync(list.Items[0].Id));

            (await WithUnitOfWorkAsync(() => _userNotificationAppService.GetUnreadCountAsync())).ShouldBe(0);
        }
    }

    // Regression test for a real gap: the customer reading their feed is a Host-realm identity with no
    // one "current" business tenant (they can belong to many at once), but UserNotification.TenantId is
    // the SENDING business's tenant — the standard IMultiTenant filter silently excluded every
    // business-originated notification the moment the reader's ambient tenant wasn't that exact
    // business. The test above never caught this because it (correctly, for what it was testing) reads
    // back inside the same _currentTenant.Change(tenantId) block used to publish — this one reproduces
    // the real customer scenario: no ambient tenant at all.
    [Fact]
    public async Task Should_Read_A_Business_Notification_With_No_Ambient_Tenant_Selected()
    {
        var tenantId = await CreateTenantAsync();
        var userId = await CreateUserAsync(tenantId);

        await WithUnitOfWorkAsync(() => _notificationPublisher.PublishToUserAsync(
            userId, tenantId, UserNotificationType.Success, "Bonus!", "You earned bonus points.", "Referral"));

        using (_currentTenant.Change(null)) // the customer's own session: Host-realm, no business selected
        using (LoginAs(userId))
        {
            (await WithUnitOfWorkAsync(() => _userNotificationAppService.GetUnreadCountAsync())).ShouldBe(1);

            var list = await WithUnitOfWorkAsync(() => _userNotificationAppService.GetListAsync(new UserNotificationListFilterDto()));
            list.TotalCount.ShouldBe(1);
            list.Items[0].Title.ShouldBe("Bonus!");

            await WithUnitOfWorkAsync(() => _userNotificationAppService.MarkAsReadAsync(list.Items[0].Id));

            (await WithUnitOfWorkAsync(() => _userNotificationAppService.GetUnreadCountAsync())).ShouldBe(0);
        }
    }

    [Fact]
    public async Task Should_Not_Let_A_User_Mark_Someone_Elses_Notification_As_Read()
    {
        var tenantId = await CreateTenantAsync();
        var ownerId = await CreateUserAsync(tenantId);
        var strangerId = await CreateUserAsync(tenantId);

        await WithUnitOfWorkAsync(() => _notificationPublisher.PublishToUserAsync(
            ownerId, tenantId, UserNotificationType.Warning, "Heads up", "Something happened."));

        Guid notificationId;
        using (_currentTenant.Change(tenantId))
        using (LoginAs(ownerId))
        {
            var list = await WithUnitOfWorkAsync(() => _userNotificationAppService.GetListAsync(new UserNotificationListFilterDto()));
            notificationId = list.Items[0].Id;
        }

        using (_currentTenant.Change(tenantId))
        using (LoginAs(strangerId))
        {
            await Assert.ThrowsAsync<AbpAuthorizationException>(() =>
                WithUnitOfWorkAsync(() => _userNotificationAppService.MarkAsReadAsync(notificationId)));
        }
    }

    [Fact]
    public async Task Should_Fan_Out_A_Tenant_Notification_To_Every_Tenant_User()
    {
        var tenantId = await CreateTenantAsync();
        var user1 = await CreateUserAsync(tenantId);
        var user2 = await CreateUserAsync(tenantId);

        await WithUnitOfWorkAsync(() => _notificationPublisher.PublishToTenantAsync(
            tenantId, UserNotificationType.Success, "Maintenance", "Completed."));

        foreach (var userId in new[] { user1, user2 })
        {
            using (_currentTenant.Change(tenantId))
            using (LoginAs(userId))
            {
                (await WithUnitOfWorkAsync(() => _userNotificationAppService.GetUnreadCountAsync())).ShouldBe(1);
            }
        }
    }

    [Fact]
    public async Task Should_Mark_All_As_Read()
    {
        var tenantId = await CreateTenantAsync();
        var userId = await CreateUserAsync(tenantId);

        await WithUnitOfWorkAsync(() => _notificationPublisher.PublishToUserAsync(userId, tenantId, UserNotificationType.Info, "A", "a"));
        await WithUnitOfWorkAsync(() => _notificationPublisher.PublishToUserAsync(userId, tenantId, UserNotificationType.Info, "B", "b"));

        using (_currentTenant.Change(tenantId))
        using (LoginAs(userId))
        {
            (await WithUnitOfWorkAsync(() => _userNotificationAppService.GetUnreadCountAsync())).ShouldBe(2);

            await WithUnitOfWorkAsync(() => _userNotificationAppService.MarkAllAsReadAsync());

            (await WithUnitOfWorkAsync(() => _userNotificationAppService.GetUnreadCountAsync())).ShouldBe(0);
        }
    }
}
