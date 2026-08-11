using System;
using System.Threading.Tasks;
using Eksabli.Wallets;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;

namespace Eksabli.Data.Seeders;

// Gives every newly created tenant a sensible starter loyalty program instead of an empty one,
// matching the demo data shown in the business portal prototype
// (prototype/business/points-management.html — "Point Rules" and "Tiers" tabs). Businesses can
// edit or delete any of this afterwards via PointRuleAppService / TierAppService; this only fills
// in defaults for a brand-new tenant so the Points Management screen isn't blank on day one.
//
// Tenant-scoped only (context.TenantId must be set): PointRule and Tier are both IMultiTenant, and
// BusinessAppService.RegisterAsync already runs this contributor inside its own
// _currentTenant.Change(tenant.Id) block via the nested
// _dataSeeder.SeedAsync(new DataSeedContext(tenant.Id)) call — the same mechanism
// IdentityDataSeedContributor uses to create the new tenant's "admin" user. That call re-discovers
// every registered IDataSeedContributor, including this one, so it applies uniformly to real
// self-serve signups and to the seeded demo business (DemoBusinessDataSeederContributor) alike.
// No-ops on the host-level pass (context.TenantId == null) — there's nothing platform-wide to seed.
public class DefaultLoyaltyProgramDataSeederContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IPointRuleRepository _pointRuleRepository;
    private readonly ITierRepository _tierRepository;

    public DefaultLoyaltyProgramDataSeederContributor(
        IPointRuleRepository pointRuleRepository,
        ITierRepository tierRepository)
    {
        _pointRuleRepository = pointRuleRepository;
        _tierRepository = tierRepository;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (context?.TenantId == null)
        {
            return;
        }

        // Independent guards (not one combined check) so partial state — e.g. a tenant that deleted
        // its point rules but kept its tiers — doesn't silently skip re-seeding the other half.
        if (await _pointRuleRepository.GetCountAsync() == 0)
        {
            await _pointRuleRepository.InsertAsync(PointRule.Create(Guid.NewGuid(), PointRuleType.PerCurrencyUnit, 1), autoSave: true);
            await _pointRuleRepository.InsertAsync(PointRule.Create(Guid.NewGuid(), PointRuleType.PerVisit, 10), autoSave: true);
        }

        if (await _tierRepository.GetCountAsync() == 0)
        {
            await _tierRepository.InsertAsync(Tier.Create(Guid.NewGuid(), "Bronze", 0, 1.0m), autoSave: true);
            await _tierRepository.InsertAsync(Tier.Create(Guid.NewGuid(), "Silver", 500, 1.1m), autoSave: true);
            await _tierRepository.InsertAsync(Tier.Create(Guid.NewGuid(), "Gold", 2000, 1.25m), autoSave: true);
            await _tierRepository.InsertAsync(Tier.Create(Guid.NewGuid(), "Platinum", 5000, 1.5m), autoSave: true);
        }
    }
}
