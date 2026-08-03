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
using Xunit;

namespace Eksabli.Engagement;

// Regression coverage for a hardening-pass fix: a referral bonus moves LifetimeEarned, so it must
// re-check CurrentTierId the same way a purchase does — otherwise Campaigns.CampaignSegmentEvaluator's
// Tier-segment targeting reads a stale tier until the wallet's next purchase.
public abstract class ReferralCompletionService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IReferralCompletionService _referralCompletionService;
    private readonly IReferralRepository _referralRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<Tier, Guid> _tierRepository;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guidGenerator;

    protected ReferralCompletionService_Tests()
    {
        _referralCompletionService = GetRequiredService<IReferralCompletionService>();
        _referralRepository = GetRequiredService<IReferralRepository>();
        _membershipRepository = GetRequiredService<IRepository<Membership, Guid>>();
        _walletRepository = GetRequiredService<IRepository<PointsWallet, Guid>>();
        _tierRepository = GetRequiredService<IRepository<Tier, Guid>>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
    }

    [Fact]
    public async Task TryCompleteAsync_Should_Recompute_Tier_For_Both_Wallets_When_Bonus_Crosses_Threshold()
    {
        Guid tenantId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            var tenant = await _tenantManager.CreateAsync("tenant-" + Guid.NewGuid().ToString("N"));
            await _tenantRepository.InsertAsync(tenant, autoSave: true);
            tenantId = tenant.Id;
        });

        Guid vipTierId = default;
        Membership refereeMembership = null!, referrerMembership = null!;
        PointsWallet refereeWallet = null!, referrerWallet = null!;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                // ReferralConsts.BonusPoints is 100 — set the threshold below that so a single bonus
                // (with LifetimeEarned starting at 0) definitely crosses it.
                var vipTier = Tier.Create(_guidGenerator.Create(), "VIP", minLifetimePoints: 50, multiplier: 1.5m);
                await _tierRepository.InsertAsync(vipTier, autoSave: true);
                vipTierId = vipTier.Id;

                referrerMembership = Membership.Create(_guidGenerator.Create(), Guid.NewGuid(), DateTime.UtcNow);
                await _membershipRepository.InsertAsync(referrerMembership, autoSave: true);
                referrerWallet = PointsWallet.Create(_guidGenerator.Create(), referrerMembership.Id);
                await _walletRepository.InsertAsync(referrerWallet, autoSave: true);

                refereeMembership = Membership.Create(_guidGenerator.Create(), Guid.NewGuid(), DateTime.UtcNow);
                await _membershipRepository.InsertAsync(refereeMembership, autoSave: true);
                refereeWallet = PointsWallet.Create(_guidGenerator.Create(), refereeMembership.Id);
                await _walletRepository.InsertAsync(refereeWallet, autoSave: true);

                var referral = Referral.Create(_guidGenerator.Create(), referrerMembership.Id, refereeMembership.CustomerId);
                await _referralRepository.InsertAsync(referral, autoSave: true);
            }
        });

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                await _referralCompletionService.TryCompleteAsync(refereeMembership, refereeWallet, isFirstEarn: true);
            }
        });

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var reloadedReferee = await _walletRepository.GetAsync(refereeWallet.Id);
                var reloadedReferrer = await _walletRepository.GetAsync(referrerWallet.Id);

                reloadedReferee.CurrentTierId.ShouldBe(vipTierId);
                reloadedReferrer.CurrentTierId.ShouldBe(vipTierId);
            }
        });
    }
}
