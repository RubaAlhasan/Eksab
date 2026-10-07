using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Eksabli.Notifications;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Notifications;

public class NotificationPreferencesTests : EksabliEntityFrameworkCoreTestBase
{
    private readonly INotificationPublisher _publisher;
    private readonly IRepository<UserNotification, Guid> _userNotificationRepository;
    private readonly IRepository<NotificationGroupOptOut, Guid> _optOutRepository;
    private readonly ICustomerNotificationPreferenceAppService _preferences;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    public NotificationPreferencesTests()
    {
        _publisher = GetRequiredService<INotificationPublisher>();
        _userNotificationRepository = GetRequiredService<IRepository<UserNotification, Guid>>();
        _optOutRepository = GetRequiredService<IRepository<NotificationGroupOptOut, Guid>>();
        _preferences = GetRequiredService<ICustomerNotificationPreferenceAppService>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private IDisposable LoginAs(Guid userId)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(AbpClaimTypes.UserId, userId.ToString()));
        return _principalAccessor.Change(new ClaimsPrincipal(identity));
    }

    private async Task<int> InboxCountAsync(Guid userId) =>
        (await _userNotificationRepository.GetListAsync(n => n.UserId == userId)).Count;

    [Fact]
    public async Task A_switched_off_group_sends_nothing_while_the_other_groups_still_arrive()
    {
        var customer = Guid.NewGuid();
        await WithUnitOfWorkAsync(() => _optOutRepository.InsertAsync(NotificationGroupOptOut.Create(Guid.NewGuid(), customer, NotificationGroup.Offers), autoSave: true));

        await WithUnitOfWorkAsync(() => _publisher.PublishToUserAsync(customer, null, UserNotificationType.Info, "Promo", "Hello", category: "Manual"));
        await WithUnitOfWorkAsync(() => _publisher.PublishToUserAsync(customer, null, UserNotificationType.Success, "Points", "+10", category: "points.earned"));

        (await WithUnitOfWorkAsync(() => InboxCountAsync(customer))).ShouldBe(1);
    }

    [Fact]
    public async Task A_customer_can_switch_groups_off_and_back_on_from_settings()
    {
        var customer = Guid.NewGuid();

        using (LoginAs(customer))
        {
            var off = await WithUnitOfWorkAsync(() => _preferences.UpdateMyAsync(new NotificationPreferencesDto { Rewards = true, Deals = false, Offers = true }));
            off.Deals.ShouldBeFalse();

            var read = await WithUnitOfWorkAsync(() => _preferences.GetMyAsync());
            read.Rewards.ShouldBeTrue();
            read.Deals.ShouldBeFalse();
            read.Offers.ShouldBeTrue();

            await WithUnitOfWorkAsync(() => _preferences.UpdateMyAsync(new NotificationPreferencesDto { Rewards = true, Deals = true, Offers = true }));
        }

        (await WithUnitOfWorkAsync(() => _optOutRepository.GetListAsync(o => o.UserId == customer))).ShouldBeEmpty();
    }
}
