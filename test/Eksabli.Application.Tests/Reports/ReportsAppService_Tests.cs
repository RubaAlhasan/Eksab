using System;
using System.Threading.Tasks;
using Eksabli.Memberships;
using Eksabli.Wallets;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Timing;
using Xunit;

namespace Eksabli.Reports;

public abstract class ReportsAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IReportsAppService _reportsAppService;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<PointsTransaction, Guid> _transactionRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IClock _clock;

    protected ReportsAppService_Tests()
    {
        _reportsAppService = GetRequiredService<IReportsAppService>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _membershipRepository = GetRequiredService<IRepository<Membership, Guid>>();
        _walletRepository = GetRequiredService<IRepository<PointsWallet, Guid>>();
        _transactionRepository = GetRequiredService<IRepository<PointsTransaction, Guid>>();
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

    private async Task<(Membership Membership, PointsWallet Wallet)> CreateMemberWithWalletAsync(DateTime joinedAt)
    {
        Membership membership = null!;
        PointsWallet wallet = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            membership = Membership.Create(_guidGenerator.Create(), Guid.NewGuid(), joinedAt);
            await _membershipRepository.InsertAsync(membership, autoSave: true);

            wallet = PointsWallet.Create(_guidGenerator.Create(), membership.Id);
            await _walletRepository.InsertAsync(wallet, autoSave: true);
        });

        return (membership, wallet);
    }

    // AuditedAggregateRoot stamps CreationTime as "now" on insert — fine here since every earn in
    // these tests is meant to land inside the trailing 30-day window.
    private async Task AddEarnTransactionAsync(Guid walletId, int points)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var transaction = PointsTransaction.Create(_guidGenerator.Create(), walletId, PointsTransactionType.Earn, points, PointsTransactionSource.Purchase);
            await _transactionRepository.InsertAsync(transaction, autoSave: true);

            var wallet = await _walletRepository.GetAsync(walletId);
            wallet.ApplyTransaction(PointsTransactionType.Earn, points);
            await _walletRepository.UpdateAsync(wallet, autoSave: true);
        });
    }

    [Fact]
    public async Task GetDashboardHomeAsync_Should_Count_Active_Members_And_Sum_Points()
    {
        var tenantId = await CreateTenantAsync();

        using (_currentTenant.Change(tenantId))
        {
            var (_, wallet) = await CreateMemberWithWalletAsync(_clock.Now.AddDays(-10));
            await AddEarnTransactionAsync(wallet.Id, 50);

            var dashboard = await WithUnitOfWorkAsync(() => _reportsAppService.GetDashboardHomeAsync());

            dashboard.ActiveMemberCount.ShouldBe(1);
            dashboard.PointsIssuedLast30Days.ShouldBe(50);
        }
    }

    [Fact]
    public async Task GetRedemptionRateAsync_Should_Compute_Rate_From_Earn_And_Redeem_Transactions()
    {
        var tenantId = await CreateTenantAsync();

        using (_currentTenant.Change(tenantId))
        {
            var (_, wallet) = await CreateMemberWithWalletAsync(_clock.Now.AddDays(-10));
            await AddEarnTransactionAsync(wallet.Id, 100);

            await WithUnitOfWorkAsync(async () =>
            {
                var redeem = PointsTransaction.Create(_guidGenerator.Create(), wallet.Id, PointsTransactionType.Redeem, -25, PointsTransactionSource.Reward);
                await _transactionRepository.InsertAsync(redeem, autoSave: true);
            });

            var report = await WithUnitOfWorkAsync(() => _reportsAppService.GetRedemptionRateAsync(new ReportPeriodDto
            {
                From = _clock.Now.AddDays(-1),
                To = _clock.Now.AddDays(1)
            }));

            report.EarnedPoints.ShouldBe(100);
            report.RedeemedPoints.ShouldBe(25);
            report.RedemptionRate.ShouldBe(0.25m);
        }
    }

    [Fact]
    public async Task GetCustomerSegmentsAsync_Should_Classify_New_Active_AtRisk_And_Churned()
    {
        var tenantId = await CreateTenantAsync();

        using (_currentTenant.Change(tenantId))
        {
            // New — joined within the trailing 30 days, no activity needed.
            await CreateMemberWithWalletAsync(_clock.Now.AddDays(-5));

            // Active — joined long ago, earned recently.
            var (_, activeWallet) = await CreateMemberWithWalletAsync(_clock.Now.AddDays(-200));
            await AddEarnTransactionAsync(activeWallet.Id, 10);

            // Churned — joined long ago, never earned.
            await CreateMemberWithWalletAsync(_clock.Now.AddDays(-200));

            var segments = await WithUnitOfWorkAsync(() => _reportsAppService.GetCustomerSegmentsAsync());

            segments.New.ShouldBe(1);
            segments.Active.ShouldBe(1);
            segments.Churned.ShouldBe(1);
            segments.AtRisk.ShouldBe(0);
        }
    }
}
