using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Modularity;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.Rewards;

public abstract class CouponAuditAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ICouponAuditAppService _couponAuditAppService;
    private readonly IRewardRepository _rewardRepository;
    private readonly ICouponRepository _couponRepository;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;

    protected CouponAuditAppService_Tests()
    {
        _couponAuditAppService = GetRequiredService<ICouponAuditAppService>();
        _rewardRepository = GetRequiredService<IRewardRepository>();
        _couponRepository = GetRequiredService<ICouponRepository>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
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

    private async Task<Guid> CreateCouponAsync(Guid tenantId, CouponStatus status)
    {
        Guid couponId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var reward = Reward.Create(Guid.NewGuid(), "مكافأة", "Reward", RewardType.Discount, 10);
                await _rewardRepository.InsertAsync(reward, autoSave: true);

                var code = Guid.NewGuid().ToString("N")[..CouponConsts.CodeLength].ToUpperInvariant();
                var coupon = Coupon.Create(Guid.NewGuid(), reward.Id, Guid.NewGuid(), code, DateTime.UtcNow);
                if (status == CouponStatus.Redeemed)
                {
                    coupon.MarkRedeemed(DateTime.UtcNow, Guid.NewGuid(), null);
                }
                await _couponRepository.InsertAsync(coupon, autoSave: true);
                couponId = coupon.Id;
            }
        });
        return couponId;
    }

    [Fact]
    public async Task Should_List_And_Filter_Coupons_By_Status()
    {
        var tenantId = await CreateTenantAsync();
        var issuedId = await CreateCouponAsync(tenantId, CouponStatus.Issued);
        var redeemedId = await CreateCouponAsync(tenantId, CouponStatus.Redeemed);

        using (_currentTenant.Change(tenantId))
        {
            var all = await WithUnitOfWorkAsync(() => _couponAuditAppService.GetListAsync(new CouponAuditFilterDto()));
            all.TotalCount.ShouldBe(2);

            var redeemedOnly = await WithUnitOfWorkAsync(() => _couponAuditAppService.GetListAsync(new CouponAuditFilterDto { Status = CouponStatus.Redeemed }));
            redeemedOnly.TotalCount.ShouldBe(1);
            redeemedOnly.Items[0].Id.ShouldBe(redeemedId);
            redeemedOnly.Items.ShouldNotContain(c => c.Id == issuedId);
        }
    }

    [Fact]
    public async Task Should_Mint_Validate_And_Burn_An_Excel_Download_Token()
    {
        var result = await WithUnitOfWorkAsync(() => _couponAuditAppService.GetDownloadTokenAsync());
        result.Token.ShouldNotBeNullOrEmpty();

        var stream = await WithUnitOfWorkAsync(() => _couponAuditAppService.GetListAsExcelFileAsync(new CouponExcelDownloadDto { DownloadToken = result.Token }));
        stream.ShouldNotBeNull();

        await Assert.ThrowsAsync<AbpAuthorizationException>(async () =>
        {
            await WithUnitOfWorkAsync(() => _couponAuditAppService.GetListAsExcelFileAsync(new CouponExcelDownloadDto { DownloadToken = "invalid-token" }));
        });
    }
}
