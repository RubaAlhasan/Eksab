using System;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Eksabli.Memberships;
using Eksabli.Notifications;
using Eksabli.Shared;
using Eksabli.SmartOffers;
using Shouldly;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.EntityFrameworkCore.SmartOffers;

public class SmartOfferPriceWatchWorkerTests : EksabliEntityFrameworkCoreTestBase
{
    private readonly SmartOfferPriceWatchWorker _worker;
    private readonly ISmartOfferAppService _offerService;
    private readonly ICustomerSmartOfferAppService _customerService;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<SmartOfferWatch, Guid> _watchRepository;
    private readonly IRepository<UserNotification, Guid> _userNotificationRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    public SmartOfferPriceWatchWorkerTests()
    {
        _worker = GetRequiredService<SmartOfferPriceWatchWorker>();
        _offerService = GetRequiredService<ISmartOfferAppService>();
        _customerService = GetRequiredService<ICustomerSmartOfferAppService>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _membershipRepository = GetRequiredService<IRepository<Membership, Guid>>();
        _watchRepository = GetRequiredService<IRepository<SmartOfferWatch, Guid>>();
        _userNotificationRepository = GetRequiredService<IRepository<UserNotification, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private IDisposable LoginAs(Guid userId)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(AbpClaimTypes.UserId, userId.ToString()));
        return _principalAccessor.Change(new ClaimsPrincipal(identity));
    }

    private async Task RunWorkerOnceAsync()
    {
        var method = typeof(SmartOfferPriceWatchWorker).GetMethod("DoWorkAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var context = new PeriodicBackgroundWorkerContext(ServiceProvider);
        await (Task)method.Invoke(_worker, new object[] { context })!;
    }

    // Notifications and watches are per business, so they are counted inside the business's own context.
    private async Task<int> InboxCountAsync(Guid tenantId, Guid customerId)
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                return (await _userNotificationRepository.GetListAsync(n => n.UserId == customerId)).Count;
            }
        });
    }

    private async Task<int> WatchCountAsync(Guid tenantId, Guid customerId)
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                return (await _watchRepository.GetListAsync(w => w.CustomerId == customerId)).Count;
            }
        });
    }

    [Fact]
    public async Task A_watched_deal_whose_price_drops_within_the_hour_tells_the_customer_once_and_is_no_longer_watched()
    {
        var now = DateTime.UtcNow;
        var boundary = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc).AddMinutes(20);

        // The stage boundary is a clock time, so the drop has to fall on the same UTC day. Within 20 minutes of midnight
        // there is no such time, and the test has nothing to check, so it passes without asserting.
        if (boundary.Date != now.Date)
        {
            return;
        }
        var boundaryText = boundary.ToString("HH:mm");

        var tenantId = Guid.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            var tenant = await _tenantManager.CreateAsync("tenant-" + Guid.NewGuid().ToString("N"));
            await _tenantRepository.InsertAsync(tenant, autoSave: true);
            tenantId = tenant.Id;
        });

        var customerId = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                await _membershipRepository.InsertAsync(Membership.Create(Guid.NewGuid(), customerId, DateTime.UtcNow), autoSave: true);
            }
        });

        // 10 USD until the boundary, 6 USD after it, all in UTC.
        var dto = new CreateUpdateSmartOfferDto
        {
            TitleAr = "برغر",
            TitleEn = "Burger Meal",
            Strategy = SmartPricingStrategy.TimeBased,
            Currency = Currency.Usd,
            BasePrice = 10m,
            TimeZoneId = "UTC",
            IsEnabled = true,
        };
        dto.Stages.Add(new CreateUpdateSmartOfferStageDto { StartTime = "00:00", EndTime = boundaryText, Price = 10m, Currency = Currency.Usd, QuantityLimit = 5 });
        dto.Stages.Add(new CreateUpdateSmartOfferStageDto { StartTime = boundaryText, EndTime = "24:00", Price = 6m, Currency = Currency.Usd, QuantityLimit = 5 });

        SmartOfferDto offer;
        using (_currentTenant.Change(tenantId))
        {
            offer = await WithUnitOfWorkAsync(() => _offerService.CreateAsync(dto));
        }

        using (LoginAs(customerId))
        {
            await WithUnitOfWorkAsync(() => _customerService.WatchPriceAsync(tenantId, offer.Id));
        }

        await RunWorkerOnceAsync();

        (await InboxCountAsync(tenantId, customerId)).ShouldBe(1);
        (await WatchCountAsync(tenantId, customerId)).ShouldBe(0);

        // A second run must not tell the customer again: the watch has already fired.
        await RunWorkerOnceAsync();
        (await InboxCountAsync(tenantId, customerId)).ShouldBe(1);
    }
}
