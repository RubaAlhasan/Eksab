using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Eksabli.Memberships;
using Eksabli.Wallets;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.Rewards;

public abstract class CouponAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ICouponAppService _couponAppService;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRewardRepository _rewardRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    protected CouponAppService_Tests()
    {
        _couponAppService = GetRequiredService<ICouponAppService>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _rewardRepository = GetRequiredService<IRewardRepository>();
        _membershipRepository = GetRequiredService<IRepository<Membership, Guid>>();
        _walletRepository = GetRequiredService<IRepository<PointsWallet, Guid>>();
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

    private async Task<Guid> JoinBusinessWithBalanceAsync(Guid tenantId, Guid customerId, int balance)
    {
        Guid membershipId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var membership = Membership.Create(Guid.NewGuid(), customerId, DateTime.UtcNow);
                await _membershipRepository.InsertAsync(membership, autoSave: true);
                membershipId = membership.Id;

                var wallet = PointsWallet.Create(Guid.NewGuid(), membership.Id);
                wallet.ApplyTransaction(PointsTransactionType.Earn, balance);
                await _walletRepository.InsertAsync(wallet, autoSave: true);
            }
        });
        return membershipId;
    }

    private async Task<Guid> CreateRewardAsync(Guid tenantId, int pointsCost, int? stockRemaining = null, DateTime? validFrom = null, DateTime? validTo = null)
    {
        Guid rewardId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var reward = Reward.Create(Guid.NewGuid(), "مكافأة", "Reward", RewardType.Discount, pointsCost);
                reward.SetStock(stockRemaining);
                reward.SetValidity(validFrom, validTo);
                await _rewardRepository.InsertAsync(reward, autoSave: true);
                rewardId = reward.Id;
            }
        });
        return rewardId;
    }

    [Fact]
    public async Task Should_Redeem_A_Reward_And_Deduct_Points_And_Decrement_Stock()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinBusinessWithBalanceAsync(tenantId, customerId, 200);
        var rewardId = await CreateRewardAsync(tenantId, pointsCost: 100, stockRemaining: 5);

        CouponDto coupon = null!;
        using (LoginAs(customerId))
        {
            coupon = await WithUnitOfWorkAsync(() => _couponAppService.RedeemAsync(new RedeemRewardDto { TenantId = tenantId, RewardId = rewardId }));
        }

        coupon.Status.ShouldBe(CouponStatus.Issued);
        coupon.Code.Length.ShouldBe(CouponConsts.CodeLength);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var membership = await _membershipRepository.SingleAsync(m => m.CustomerId == customerId);
                var wallet = await _walletRepository.SingleAsync(w => w.MembershipId == membership.Id);
                wallet.Balance.ShouldBe(100);

                var reward = await _rewardRepository.GetAsync(rewardId);
                reward.StockRemaining.ShouldBe(4);
            }
        });
    }

    [Fact]
    public async Task Should_Reject_Redemption_When_Balance_Is_Insufficient()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinBusinessWithBalanceAsync(tenantId, customerId, 10);
        var rewardId = await CreateRewardAsync(tenantId, pointsCost: 100);

        using (LoginAs(customerId))
        {
            await Assert.ThrowsAsync<UserFriendlyException>(async () =>
            {
                await WithUnitOfWorkAsync(() => _couponAppService.RedeemAsync(new RedeemRewardDto { TenantId = tenantId, RewardId = rewardId }));
            });
        }
    }

    [Fact]
    public async Task Should_Reject_Redemption_For_A_NonMember_Without_Auto_Joining()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid(); // never joins
        var rewardId = await CreateRewardAsync(tenantId, pointsCost: 10);

        using (LoginAs(customerId))
        {
            await Assert.ThrowsAsync<UserFriendlyException>(async () =>
            {
                await WithUnitOfWorkAsync(() => _couponAppService.RedeemAsync(new RedeemRewardDto { TenantId = tenantId, RewardId = rewardId }));
            });
        }
    }

    [Fact]
    public async Task Should_Reject_Redemption_When_Stock_Is_Exhausted()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinBusinessWithBalanceAsync(tenantId, customerId, 200);
        var rewardId = await CreateRewardAsync(tenantId, pointsCost: 10, stockRemaining: 0);

        using (LoginAs(customerId))
        {
            await Assert.ThrowsAsync<UserFriendlyException>(async () =>
            {
                await WithUnitOfWorkAsync(() => _couponAppService.RedeemAsync(new RedeemRewardDto { TenantId = tenantId, RewardId = rewardId }));
            });
        }
    }

    [Fact]
    public async Task Should_Reject_Redemption_Outside_The_Validity_Window()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinBusinessWithBalanceAsync(tenantId, customerId, 200);
        var rewardId = await CreateRewardAsync(tenantId, pointsCost: 10, validTo: DateTime.UtcNow.AddDays(-1));

        using (LoginAs(customerId))
        {
            await Assert.ThrowsAsync<UserFriendlyException>(async () =>
            {
                await WithUnitOfWorkAsync(() => _couponAppService.RedeemAsync(new RedeemRewardDto { TenantId = tenantId, RewardId = rewardId }));
            });
        }
    }

    [Fact]
    public async Task Should_Aggregate_My_Coupons_Across_Two_Tenants()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinBusinessWithBalanceAsync(tenantA, customerId, 100);
        await JoinBusinessWithBalanceAsync(tenantB, customerId, 100);
        var rewardA = await CreateRewardAsync(tenantA, pointsCost: 10);
        var rewardB = await CreateRewardAsync(tenantB, pointsCost: 10);

        using (LoginAs(customerId))
        {
            await WithUnitOfWorkAsync(() => _couponAppService.RedeemAsync(new RedeemRewardDto { TenantId = tenantA, RewardId = rewardA }));
            await WithUnitOfWorkAsync(() => _couponAppService.RedeemAsync(new RedeemRewardDto { TenantId = tenantB, RewardId = rewardB }));

            var coupons = await WithUnitOfWorkAsync(() => _couponAppService.GetMyCouponsAsync());
            coupons.Count.ShouldBe(2);
            coupons.Select(c => c.TenantId).ShouldBe(new Guid?[] { tenantA, tenantB }, ignoreOrder: true);
        }
    }
}
