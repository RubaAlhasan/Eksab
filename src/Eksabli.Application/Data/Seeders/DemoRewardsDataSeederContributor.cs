using System;
using System.Threading.Tasks;
using Eksabli.Rewards;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace Eksabli.Data.Seeders;

// StarbucksDemo (and, until DemoBookshopDataSeederContributor, every other demo business) shipped
// with zero rewards — the customer-facing redeem flow (rewards catalog -> redeem -> coupon) was
// therefore untestable against real seeded data, only against whatever a developer happened to add by
// hand through the Business Portal first. Seeded via the raw repository (not IRewardAppService,
// which is [Authorize]-gated for business staff — there is no signed-in user during data seeding),
// same reasoning as DefaultLoyaltyProgramDataSeederContributor seeding Tier/PointRule directly.
// Content mirrors the prototype's own demo rewards for these exact businesses
// (prototype/assets/js/demo-data.js: rew_1-3 for Cedar & Bean / biz_1, rew_6-7 for Cornerstone
// Bookshop / biz_4) rather than arbitrary numbers.
//
// Host-level pass only: needs to look up both demo tenants by name and switch CurrentTenant into
// each in turn, so it can't run as a normal tenant-scoped contributor the way DefaultLoyaltyProgram's
// does.
[DependsOn(typeof(DemoBusinessDataSeederContributor), typeof(DemoBookshopDataSeederContributor))]
public class DemoRewardsDataSeederContributor : IDataSeedContributor, ITransientDependency
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<Reward, Guid> _rewardRepository;
    private readonly ICurrentTenant _currentTenant;

    public DemoRewardsDataSeederContributor(
        ITenantRepository tenantRepository,
        IRepository<Reward, Guid> rewardRepository,
        ICurrentTenant currentTenant)
    {
        _tenantRepository = tenantRepository;
        _rewardRepository = rewardRepository;
        _currentTenant = currentTenant;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (context?.TenantId != null)
        {
            return;
        }

        await SeedForTenantAsync(DemoBusinessDataSeederContributor.DemoBusinessName, SeedStarbucksRewardsAsync);
        await SeedForTenantAsync(DemoBookshopDataSeederContributor.DemoBusinessName, SeedBookshopRewardsAsync);
    }

    private async Task SeedForTenantAsync(string tenantName, Func<Task> seedRewards)
    {
        var tenant = await _tenantRepository.FindByNameAsync(tenantName.Normalize());
        if (tenant == null)
        {
            return;
        }

        using (_currentTenant.Change(tenant.Id))
        {
            // Independent of DemoBusinessDataSeederContributor's own "already exists" branch — that one
            // only re-runs the loyalty-program defaults, so a demo tenant created before this contributor
            // existed would otherwise never get backfilled with rewards either.
            if (await _rewardRepository.GetCountAsync() == 0)
            {
                await seedRewards();
            }
        }
    }

    private async Task SeedStarbucksRewardsAsync()
    {
        await _rewardRepository.InsertAsync(WithStock(
            Reward.Create(Guid.NewGuid(), "قهوة كبيرة مجانية", "Free Large Coffee", RewardType.FreeProduct, 500), 50), autoSave: true);
        await _rewardRepository.InsertAsync(
            Reward.Create(Guid.NewGuid(), "خصم 15% على أي طلب", "15% Off Any Order", RewardType.Discount, 350), autoSave: true);
        await _rewardRepository.InsertAsync(WithStock(
            Reward.Create(Guid.NewGuid(), "بطاقة هدايا بقيمة 25$", "$25 Gift Card", RewardType.GiftCard, 2000), 10), autoSave: true);
    }

    private async Task SeedBookshopRewardsAsync()
    {
        await _rewardRepository.InsertAsync(
            Reward.Create(Guid.NewGuid(), "خصم 10$ على الطلب القادم", "$10 Off Next Purchase", RewardType.Discount, 150), autoSave: true);
        await _rewardRepository.InsertAsync(WithStock(
            Reward.Create(Guid.NewGuid(), "إشارة كتاب وحقيبة توتري مجانية", "Free Bookmark & Tote Bag", RewardType.FreeProduct, 90), 100), autoSave: true);
    }

    private static Reward WithStock(Reward reward, int stock)
    {
        reward.SetStock(stock);
        return reward;
    }
}
