using System;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.BusinessProfiles;
using Eksabli.Memberships;
using Eksabli.SmartOffers;
using Eksabli.Shared;
using Eksabli.Wallets;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Timing;
using Xunit;

namespace Eksabli.Dashboards;

public abstract class BusinessDashboardAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IBusinessDashboardAppService _dashboard;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<BusinessProfile, Guid> _profileRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<PointsTransaction, Guid> _transactionRepository;
    private readonly ISmartOfferRepository _offerRepository;
    private readonly IRepository<SmartOfferOrder, Guid> _orderRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IClock _clock;

    protected BusinessDashboardAppService_Tests()
    {
        _dashboard = GetRequiredService<IBusinessDashboardAppService>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _profileRepository = GetRequiredService<IRepository<BusinessProfile, Guid>>();
        _membershipRepository = GetRequiredService<IRepository<Membership, Guid>>();
        _walletRepository = GetRequiredService<IRepository<PointsWallet, Guid>>();
        _transactionRepository = GetRequiredService<IRepository<PointsTransaction, Guid>>();
        _offerRepository = GetRequiredService<ISmartOfferRepository>();
        _orderRepository = GetRequiredService<IRepository<SmartOfferOrder, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
        _clock = GetRequiredService<IClock>();
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

    // A customer's membership and wallet at the current tenant. Returns the wallet id, which is what ledger rows point at.
    private async Task<Guid> CreateWalletAsync()
    {
        var walletId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            var membership = Membership.Create(_guidGenerator.Create(), Guid.NewGuid(), _clock.Now);
            await _membershipRepository.InsertAsync(membership, autoSave: true);

            var wallet = PointsWallet.Create(_guidGenerator.Create(), membership.Id);
            await _walletRepository.InsertAsync(wallet, autoSave: true);
            walletId = wallet.Id;
        });

        return walletId;
    }

    // Purchase awards write a base Purchase row carrying the sale amount, the same way PosAppService does.
    private async Task AddPurchaseAsync(Guid walletId, int points, decimal? amount = null, Currency? currency = null)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var transaction = PointsTransaction.Create(
                _guidGenerator.Create(),
                walletId,
                PointsTransactionType.Earn,
                points,
                PointsTransactionSource.Purchase,
                batchId: _guidGenerator.Create(),
                amount: amount,
                currency: currency);
            await _transactionRepository.InsertAsync(transaction, autoSave: true);
        });
    }

    private async Task<SmartOffer> CreateLiveFixedOfferAsync(decimal price)
    {
        var offer = SmartOffer.Create(
            _guidGenerator.Create(),
            new SmartOfferDetails("عرض", "Deal", null, null),
            new SmartOfferPricingSpec(SmartPricingStrategy.Fixed, Currency.Usd, price, null, null, Array.Empty<SmartOfferStageSpec>()),
            new SmartOfferScheduleSpec("Asia/Damascus", null, null),
            isEnabled: true);

        await WithUnitOfWorkAsync(async () => await _offerRepository.InsertAsync(offer, autoSave: true));
        return offer;
    }

    // Places a Buy Now order against the offer and returns it, still Pending.
    private async Task<SmartOfferOrder> PlaceOrderAsync(SmartOffer offer, Guid membershipId, decimal price)
    {
        var now = _clock.Now;
        var quote = new SmartPriceQuote(
            SlotId: Guid.NewGuid(),
            StageId: null,
            Price: price,
            BasePrice: price,
            Currency: Currency.Usd,
            WindowStartUtc: now.AddMinutes(-5),
            WindowEndUtc: now.AddHours(1),
            QuantityLimit: null);

        var order = SmartOfferOrder.Place(
            _guidGenerator.Create(),
            offer.Id,
            membershipId,
            Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            offer.TitleAr,
            offer.TitleEn,
            quote,
            quantity: 1,
            serviceDate: DateOnly.FromDateTime(now),
            placedAt: now);

        await WithUnitOfWorkAsync(async () => await _orderRepository.InsertAsync(order, autoSave: true));
        return order;
    }

    [Fact]
    public async Task GetSummaryAsync_Should_Only_Count_The_Callers_Own_Tenant()
    {
        var mine = await CreateTenantAsync();
        var theirs = await CreateTenantAsync();

        using (_currentTenant.Change(mine))
        {
            await AddPurchaseAsync(await CreateWalletAsync(), 100, 50m, Currency.Usd);
        }

        using (_currentTenant.Change(theirs))
        {
            await AddPurchaseAsync(await CreateWalletAsync(), 900, 999m, Currency.Usd);
        }

        using (_currentTenant.Change(mine))
        {
            var summary = await WithUnitOfWorkAsync(() => _dashboard.GetSummaryAsync(new DashboardRangeDto()));

            summary.PointsIssued.ShouldBe(100);
            summary.Transactions.ShouldBe(1);
            summary.RecordedValue.ShouldHaveSingleItem().Amount.ShouldBe(50m);
        }
    }

    [Fact]
    public async Task GetSummaryAsync_Should_Keep_SYP_And_USD_Value_Separate()
    {
        var tenantId = await CreateTenantAsync();

        using (_currentTenant.Change(tenantId))
        {
            var wallet = await CreateWalletAsync();
            await AddPurchaseAsync(wallet, 10, 100_000m, Currency.Syp);
            await AddPurchaseAsync(wallet, 10, 40m, Currency.Usd);

            var summary = await WithUnitOfWorkAsync(() => _dashboard.GetSummaryAsync(new DashboardRangeDto()));

            // One entry per currency. Summing 100,000 SYP with 40 USD would be meaningless.
            summary.RecordedValue.Count.ShouldBe(2);
            summary.RecordedValue.Single(v => v.Currency == Currency.Syp).Amount.ShouldBe(100_000m);
            summary.RecordedValue.Single(v => v.Currency == Currency.Usd).Amount.ShouldBe(40m);
        }
    }

    [Fact]
    public async Task GetSummaryAsync_Should_Lower_Value_Coverage_When_A_Purchase_Has_No_Amount()
    {
        var tenantId = await CreateTenantAsync();

        using (_currentTenant.Change(tenantId))
        {
            var wallet = await CreateWalletAsync();
            await AddPurchaseAsync(wallet, 10, 20m, Currency.Usd);
            await AddPurchaseAsync(wallet, 10);

            var summary = await WithUnitOfWorkAsync(() => _dashboard.GetSummaryAsync(new DashboardRangeDto()));

            summary.Transactions.ShouldBe(2);
            summary.PurchasesWithAmount.ShouldBe(1);
            summary.ValueCoveragePercent.ShouldBe(50m);
        }
    }

    [Fact]
    public async Task GetSummaryAsync_Should_Return_Null_Rates_When_The_Window_Is_Empty()
    {
        var tenantId = await CreateTenantAsync();

        using (_currentTenant.Change(tenantId))
        {
            var summary = await WithUnitOfWorkAsync(() => _dashboard.GetSummaryAsync(new DashboardRangeDto()));

            // An empty window must read as "no data", not as 0% (which would look like a real result).
            summary.RedemptionRate.ShouldBeNull();
            summary.ValueCoveragePercent.ShouldBeNull();
            summary.RecordedValue.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task GetSummaryAsync_Should_Count_New_Members_And_Customers_Served_Today()
    {
        var tenantId = await CreateTenantAsync();

        using (_currentTenant.Change(tenantId))
        {
            var wallet = await CreateWalletAsync();
            await AddPurchaseAsync(wallet, 25);

            var summary = await WithUnitOfWorkAsync(() => _dashboard.GetSummaryAsync(new DashboardRangeDto()));

            summary.NewMembers.ShouldBe(1);
            summary.ActiveMembers.ShouldBe(1);
            summary.TodaySummary.CustomersServed.ShouldBe(1);
            summary.TodaySummary.NewCustomers.ShouldBe(1);
            summary.TodaySummary.PointsIssued.ShouldBe(25);
        }
    }

    [Fact]
    public async Task GetSummaryAsync_Should_Report_The_Business_Time_Zone_And_Local_Today()
    {
        var tenantId = await CreateTenantAsync();

        using (_currentTenant.Change(tenantId))
        {
            var profile = BusinessProfile.Create(_guidGenerator.Create());
            profile.SetTimeZone("America/New_York");
            await WithUnitOfWorkAsync(async () => await _profileRepository.InsertAsync(profile, autoSave: true));

            var summary = await WithUnitOfWorkAsync(() => _dashboard.GetSummaryAsync(new DashboardRangeDto()));

            summary.TimeZoneId.ShouldBe("America/New_York");

            // "Today" is the date on the business's clock, which can differ from the server's UTC date.
            var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
            summary.Today.ShouldBe(DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(_clock.Now, newYork)));
        }
    }

    [Fact]
    public async Task GetSummaryAsync_Should_Count_Live_Offers_And_Pending_Buy_Now_Orders()
    {
        var tenantId = await CreateTenantAsync();

        using (_currentTenant.Change(tenantId))
        {
            var offer = await CreateLiveFixedOfferAsync(10m);
            var membership = Guid.NewGuid();
            await PlaceOrderAsync(offer, membership, 10m);

            var summary = await WithUnitOfWorkAsync(() => _dashboard.GetSummaryAsync(new DashboardRangeDto()));

            summary.LiveOffers.ShouldBe(1);
            summary.PendingBuyNowOrders.ShouldBe(1);
        }
    }

    [Fact]
    public async Task GetOfferPerformanceAsync_Should_Show_Completed_Sales_And_Completion_Rate()
    {
        var tenantId = await CreateTenantAsync();

        using (_currentTenant.Change(tenantId))
        {
            var offer = await CreateLiveFixedOfferAsync(10m);
            var order = await PlaceOrderAsync(offer, Guid.NewGuid(), 10m);

            order.Complete(_clock.Now, Guid.NewGuid(), null);
            await WithUnitOfWorkAsync(async () => await _orderRepository.UpdateAsync(order, autoSave: true));

            var performance = await WithUnitOfWorkAsync(() => _dashboard.GetOfferPerformanceAsync(new DashboardRangeDto()));

            var row = performance.Single(p => p.OfferId == offer.Id);
            row.Placed.ShouldBe(1);
            row.Completed.ShouldBe(1);
            row.Pending.ShouldBe(0);
            row.CompletionRate.ShouldBe(1m);
            row.CompletedValue.ShouldHaveSingleItem().Amount.ShouldBe(10m);
        }
    }

    [Fact]
    public async Task GetOfferPerformanceAsync_Should_List_Offers_With_No_Orders()
    {
        var tenantId = await CreateTenantAsync();

        using (_currentTenant.Change(tenantId))
        {
            var quiet = await CreateLiveFixedOfferAsync(5m);

            var performance = await WithUnitOfWorkAsync(() => _dashboard.GetOfferPerformanceAsync(new DashboardRangeDto()));

            // A deal nobody bought is still shown, with zeros and no completion rate, rather than disappearing.
            var row = performance.Single(p => p.OfferId == quiet.Id);
            row.Placed.ShouldBe(0);
            row.CompletionRate.ShouldBeNull();
            row.LastOrderAt.ShouldBeNull();
        }
    }

    [Fact]
    public async Task GetInsightsAsync_Should_Flag_A_Live_Offer_With_No_Recent_Orders()
    {
        var tenantId = await CreateTenantAsync();

        using (_currentTenant.Change(tenantId))
        {
            var quiet = await CreateLiveFixedOfferAsync(5m);

            var insights = await WithUnitOfWorkAsync(() => _dashboard.GetInsightsAsync());

            var insight = insights.Single(i => i.Kind == BusinessInsightKind.OfferWithoutOrders && i.RelatedId == quiet.Id);
            insight.NameEn.ShouldBe("Deal");
        }
    }

    [Fact]
    public async Task GetTrendsAsync_Should_Zero_Fill_Every_Day_In_The_Range()
    {
        var tenantId = await CreateTenantAsync();

        using (_currentTenant.Change(tenantId))
        {
            var today = DateOnly.FromDateTime(_clock.Now);
            var trend = await WithUnitOfWorkAsync(() => _dashboard.GetTrendsAsync(new DashboardRangeDto
            {
                From = today.AddDays(-6),
                To = today,
            }));

            trend.Count.ShouldBe(7);
            trend.First().Date.ShouldBe(today.AddDays(-6));
            trend.Last().Date.ShouldBe(today);
            trend.All(p => p.PointsIssued == 0).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task GetPeakHoursAsync_Should_Return_A_Full_Seven_By_Twenty_Four_Grid()
    {
        var tenantId = await CreateTenantAsync();

        using (_currentTenant.Change(tenantId))
        {
            await AddPurchaseAsync(await CreateWalletAsync(), 5, 10m, Currency.Usd);

            var grid = await WithUnitOfWorkAsync(() => _dashboard.GetPeakHoursAsync(new DashboardRangeDto()));

            grid.Count.ShouldBe(7 * 24);
            grid.Select(c => (c.DayOfWeek, c.Hour)).Distinct().Count().ShouldBe(7 * 24);
            grid.Sum(c => c.Activity).ShouldBe(1);
        }
    }

    [Fact]
    public async Task GetSummaryAsync_Should_Reject_A_Range_That_Ends_Before_It_Starts()
    {
        var tenantId = await CreateTenantAsync();

        using (_currentTenant.Change(tenantId))
        {
            var today = DateOnly.FromDateTime(_clock.Now);

            await Should.ThrowAsync<UserFriendlyException>(() => WithUnitOfWorkAsync(() => _dashboard.GetSummaryAsync(new DashboardRangeDto
            {
                From = today,
                To = today.AddDays(-1),
            })));
        }
    }

    [Fact]
    public async Task GetSummaryAsync_Should_Reject_A_Range_Longer_Than_The_Maximum()
    {
        var tenantId = await CreateTenantAsync();

        using (_currentTenant.Change(tenantId))
        {
            var today = DateOnly.FromDateTime(_clock.Now);

            await Should.ThrowAsync<UserFriendlyException>(() => WithUnitOfWorkAsync(() => _dashboard.GetSummaryAsync(new DashboardRangeDto
            {
                From = today.AddDays(-400),
                To = today,
            })));
        }
    }
}
