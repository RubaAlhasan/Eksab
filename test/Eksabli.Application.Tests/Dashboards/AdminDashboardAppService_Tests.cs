using System;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.BusinessProfiles;
using Eksabli.Memberships;
using Eksabli.Shared;
using Eksabli.Wallets;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Timing;
using Xunit;

namespace Eksabli.Dashboards;

public abstract class AdminDashboardAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAdminDashboardAppService _dashboard;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<BusinessProfile, Guid> _profileRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<PointsTransaction, Guid> _transactionRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IClock _clock;

    protected AdminDashboardAppService_Tests()
    {
        _dashboard = GetRequiredService<IAdminDashboardAppService>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _profileRepository = GetRequiredService<IRepository<BusinessProfile, Guid>>();
        _membershipRepository = GetRequiredService<IRepository<Membership, Guid>>();
        _walletRepository = GetRequiredService<IRepository<PointsWallet, Guid>>();
        _transactionRepository = GetRequiredService<IRepository<PointsTransaction, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
        _clock = GetRequiredService<IClock>();
    }

    // Creates a tenant with a business profile (Pending by default) and returns the tenant id.
    private async Task<Guid> CreateBusinessAsync(string? displayName = null)
    {
        Guid tenantId = default;

        await WithUnitOfWorkAsync(async () =>
        {
            var tenant = await _tenantManager.CreateAsync("tenant-" + Guid.NewGuid().ToString("N"));
            await _tenantRepository.InsertAsync(tenant, autoSave: true);
            tenantId = tenant.Id;
        });

        using (_currentTenant.Change(tenantId))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var profile = BusinessProfile.Create(_guidGenerator.Create());
                profile.SetDisplayName(displayName);
                await _profileRepository.InsertAsync(profile, autoSave: true);
            });
        }

        return tenantId;
    }

    private async Task AddPurchaseAsync(Guid tenantId, int points, decimal? amount = null, Currency? currency = null)
    {
        using (_currentTenant.Change(tenantId))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var membership = Membership.Create(_guidGenerator.Create(), Guid.NewGuid(), _clock.Now);
                await _membershipRepository.InsertAsync(membership, autoSave: true);

                var wallet = PointsWallet.Create(_guidGenerator.Create(), membership.Id);
                await _walletRepository.InsertAsync(wallet, autoSave: true);

                var transaction = PointsTransaction.Create(
                    _guidGenerator.Create(),
                    wallet.Id,
                    PointsTransactionType.Earn,
                    points,
                    PointsTransactionSource.Purchase,
                    batchId: _guidGenerator.Create(),
                    amount: amount,
                    currency: currency);
                await _transactionRepository.InsertAsync(transaction, autoSave: true);
            });
        }
    }

    [Fact]
    public async Task GetSummaryAsync_Should_Sum_Points_Across_Every_Business()
    {
        var before = await WithUnitOfWorkAsync(() => _dashboard.GetSummaryAsync(new DashboardRangeDto()));

        var first = await CreateBusinessAsync();
        var second = await CreateBusinessAsync();
        await AddPurchaseAsync(first, 300);
        await AddPurchaseAsync(second, 120);

        var after = await WithUnitOfWorkAsync(() => _dashboard.GetSummaryAsync(new DashboardRangeDto()));

        // Host-wide: the platform total includes every tenant, not just one.
        (after.PointsIssued - before.PointsIssued).ShouldBe(420);
        (after.Transactions - before.Transactions).ShouldBe(2);
    }

    [Fact]
    public async Task GetSummaryAsync_Should_Count_Businesses_By_Approval_Status()
    {
        var before = await WithUnitOfWorkAsync(() => _dashboard.GetSummaryAsync(new DashboardRangeDto()));

        await CreateBusinessAsync();

        var after = await WithUnitOfWorkAsync(() => _dashboard.GetSummaryAsync(new DashboardRangeDto()));

        (after.BusinessesPending - before.BusinessesPending).ShouldBe(1);
        (after.BusinessesApproved - before.BusinessesApproved).ShouldBe(0);
    }

    [Fact]
    public async Task GetSummaryAsync_Should_Include_Revenue_For_A_Caller_With_Permission()
    {
        var tenantId = await CreateBusinessAsync();
        await AddPurchaseAsync(tenantId, 10, 75m, Currency.Syp);

        var summary = await WithUnitOfWorkAsync(() => _dashboard.GetSummaryAsync(new DashboardRangeDto()));

        // The test host grants every permission, so the revenue section is present. The null-when-denied path is
        // covered by the permission gate itself and isn't exercised here.
        summary.RecordedValue.ShouldNotBeNull();
        summary.RecordedValue!.Where(v => v.Currency == Currency.Syp).Sum(v => v.Amount).ShouldBeGreaterThanOrEqualTo(75m);
    }

    [Fact]
    public async Task GetTopBusinessesAsync_Should_Rank_By_Points_Issued_Descending()
    {
        var big = await CreateBusinessAsync("Big Cafe");
        var small = await CreateBusinessAsync("Small Cafe");
        await AddPurchaseAsync(big, 5_000);
        await AddPurchaseAsync(small, 1);

        var top = await WithUnitOfWorkAsync(() => _dashboard.GetTopBusinessesAsync(new DashboardRangeDto()));

        top.Select(t => t.PointsIssued).ShouldBe(top.Select(t => t.PointsIssued).OrderByDescending(p => p).ToList());
        var bigRow = top.Single(t => t.TenantId == big);
        bigRow.Name.ShouldBe("Big Cafe");
        bigRow.PointsIssued.ShouldBeGreaterThanOrEqualTo(5_000);
    }

    [Fact]
    public async Task GetAlertsAsync_Should_Count_New_Pending_Approvals()
    {
        var before = await WithUnitOfWorkAsync(() => _dashboard.GetAlertsAsync());

        await CreateBusinessAsync();

        var after = await WithUnitOfWorkAsync(() => _dashboard.GetAlertsAsync());

        (after.PendingApprovals - before.PendingApprovals).ShouldBe(1);
    }

    [Fact]
    public async Task GetActivityAsync_Should_Include_A_New_Business_Registration()
    {
        await CreateBusinessAsync("Brand New Bakery");

        var activity = await WithUnitOfWorkAsync(() => _dashboard.GetActivityAsync());

        activity.ShouldContain(a => a.Kind == AdminActivityKind.BusinessRegistered && a.Subject == "Brand New Bakery");
        activity.Select(a => a.OccurredAt).ShouldBe(activity.Select(a => a.OccurredAt).OrderByDescending(t => t).ToList());
    }

    [Fact]
    public async Task GetTrendsAsync_Should_Zero_Fill_Every_Day_In_The_Range()
    {
        var today = DateOnly.FromDateTime(_clock.Now);

        var trend = await WithUnitOfWorkAsync(() => _dashboard.GetTrendsAsync(new DashboardRangeDto
        {
            From = today.AddDays(-4),
            To = today,
        }));

        trend.Count.ShouldBe(5);
        trend.Select(p => p.Date).ShouldBe(Enumerable.Range(0, 5).Select(i => today.AddDays(i - 4)).ToList());
    }
}
