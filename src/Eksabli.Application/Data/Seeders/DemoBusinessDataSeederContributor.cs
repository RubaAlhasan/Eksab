using System;
using System.Threading.Tasks;
using Eksabli.Billing;
using Eksabli.Businesses;
using Eksabli.BusinessProfiles;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace Eksabli.Data.Seeders;

// Seeds one demo business (tenant) + branch + owner through the exact same RegisterAsync flow a
// real self-serve signup uses (BusinessAppService.RegisterAsync — creates the Tenant, seeds its
// owner Identity user via IDataSeeder, creates BusinessProfile + Branch + EmployeeAssignment, and
// provisions a trial subscription). This gives local/dev environments and the mobile Postman
// collection (postman/Eksabli-Mobile-API.postman_collection.json) a real business+branch to test
// against — the collection's example responses already reference a "Starbucks"-themed demo business
// (see postman/gen/mobile.js's IDS.tenantStarbucks / businessId, branchId env values), this is that
// business, seeded for real rather than only existing as illustrative example JSON.
// Idempotent: skips if a tenant with this name already exists. Must run after
// SubscriptionPlanDataSeederContributor — RegisterAsync provisions a trial subscription and
// requires a plan flagged IsTrialDefault to already exist (DbMigrator's IDataSeeder pass discovers
// contributors via DI in no guaranteed order otherwise; SeedService on the Host side already calls
// this last explicitly).
[DependsOn(typeof(SubscriptionPlanDataSeederContributor))]
public class DemoBusinessDataSeederContributor : IDataSeedContributor, ITransientDependency
{
    public const string DemoBusinessName = "Starbucks Demo";
    public const string DemoOwnerEmail = "owner@starbucks-demo.eksabli.test";
    public const string DemoOwnerPassword = "1q2w3E*";
    public const string DemoBranchName = "Downtown Branch";

    private readonly ITenantRepository _tenantRepository;
    private readonly IBusinessAppService _businessAppService;
    private readonly IRepository<BusinessProfile, Guid> _businessProfileRepository;
    private readonly IDataFilter _dataFilter;

    public DemoBusinessDataSeederContributor(
        ITenantRepository tenantRepository,
        IBusinessAppService businessAppService,
        IRepository<BusinessProfile, Guid> businessProfileRepository,
        IDataFilter dataFilter)
    {
        _tenantRepository = tenantRepository;
        _businessAppService = businessAppService;
        _businessProfileRepository = businessProfileRepository;
        _dataFilter = dataFilter;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        // RegisterAsync provisions the new tenant via its own nested _dataSeeder.SeedAsync(new
        // DataSeedContext(tenant.Id)) call, which re-discovers and re-invokes every
        // IDataSeedContributor — including this one. Without this guard that recurses back into
        // RegisterAsync a second time for the same business, before the outer insert is visible to
        // the nested read, causing a duplicate-name/duplicate-grant failure. Only act on the
        // top-level host pass (TenantId == null); always no-op inside a tenant-scoped pass.
        if (context?.TenantId != null)
        {
            return;
        }

        if (await _tenantRepository.FindByNameAsync(DemoBusinessName) != null)
        {
            return;
        }

        var result = await _businessAppService.RegisterAsync(new RegisterBusinessDto
        {
            BusinessName = DemoBusinessName,
            DescriptionAr = "مقهى تجريبي لأغراض الاختبار",
            DescriptionEn = "Demo coffee shop for local testing and the mobile Postman collection.",
            BranchName = DemoBranchName,
            BranchAddress = "123 Main Street",
            BranchPhone = "+966500000000",
            OwnerEmail = DemoOwnerEmail,
            OwnerPassword = DemoOwnerPassword
        });

        // Every real registration starts TenantApprovalStatus.Pending — a manual moderation queue
        // (see BusinessProfile.ApprovalStatus). Skipping that queue here so the demo business is
        // immediately usable for testing (same as AdminTenantAppService.ApproveAsync would do), same
        // IDataFilter.Disable<IMultiTenant>() scope that service uses, since we're host-side here.
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var businessProfile = await _businessProfileRepository.GetAsync(result.BusinessProfileId);
            businessProfile.Approve();
            await _businessProfileRepository.UpdateAsync(businessProfile);
        }
    }
}
