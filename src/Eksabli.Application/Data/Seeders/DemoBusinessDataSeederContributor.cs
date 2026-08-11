using System;
using System.Threading.Tasks;
using Eksabli.Billing;
using Eksabli.Businesses;
using Eksabli.BusinessProfiles;
using Volo.Abp;
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
    // No space — a space in this name was never load-bearing, but it made this exact string easy to
    // typo/mistype differently across the places that reference it (e.g. hand-typed while
    // troubleshooting a tenant lookup); renamed for that reason, not because a space was ever broken
    // here (verified directly against the DB: Tenant.FindByNameAsync matched this string correctly
    // either way — Name/NormalizedName were clean, no stray whitespace). The existing tenant row in
    // the database has been renamed to match (Name + NormalizedName), not just this constant.
    public const string DemoBusinessName = "StarbucksDemo";
    public const string DemoOwnerEmail = "owner@starbucks-demo.eksabli.test";
    public const string DemoOwnerPassword = "1q2w3E*";
    public const string DemoBranchName = "Downtown Branch";

    // No public constant exposed by Volo.Abp.TenantManagement for this — AbpTenantValidator throws it
    // as a literal string. Used below to recover from the race described there.
    private const string DuplicateTenantNameErrorCode = "Volo.Abp.TenantManagement:DuplicateTenantName";

    private readonly ITenantRepository _tenantRepository;
    private readonly IBusinessAppService _businessAppService;
    private readonly IRepository<BusinessProfile, Guid> _businessProfileRepository;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentTenant _currentTenant;
    private readonly DefaultLoyaltyProgramDataSeederContributor _defaultLoyaltyProgramDataSeederContributor;

    public DemoBusinessDataSeederContributor(
        ITenantRepository tenantRepository,
        IBusinessAppService businessAppService,
        IRepository<BusinessProfile, Guid> businessProfileRepository,
        IDataFilter dataFilter,
        ICurrentTenant currentTenant,
        DefaultLoyaltyProgramDataSeederContributor defaultLoyaltyProgramDataSeederContributor)
    {
        _tenantRepository = tenantRepository;
        _businessAppService = businessAppService;
        _businessProfileRepository = businessProfileRepository;
        _dataFilter = dataFilter;
        _currentTenant = currentTenant;
        _defaultLoyaltyProgramDataSeederContributor = defaultLoyaltyProgramDataSeederContributor;
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

        var existingTenant = await _tenantRepository.FindByNameAsync(DemoBusinessName.Normalize());
        if (existingTenant != null)
        {
            // Tenant already registered by an earlier run. DefaultLoyaltyProgramDataSeederContributor
            // normally only runs automatically at tenant-creation time (inside RegisterAsync's nested
            // seed pass below), so a demo tenant created before that contributor existed would
            // otherwise never get backfilled — make sure it has its loyalty program defaults too.
            using (_currentTenant.Change(existingTenant.Id))
            {
                await _defaultLoyaltyProgramDataSeederContributor.SeedAsync(new DataSeedContext(existingTenant.Id));
            }
            return;
        }

        BusinessRegistrationResultDto result;
        try
        {
            result = await _businessAppService.RegisterAsync(new RegisterBusinessDto
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
        }
        catch (BusinessException ex) when (ex.Code == DuplicateTenantNameErrorCode)
        {
            // Lost a race: something else (the Host's SeedService startup task and the DbMigrator's
            // IDataSeeder pass can both run this contributor around the same time — see SeedService.cs)
            // created the tenant between our FindByNameAsync check above and TenantManager.CreateAsync
            // inside RegisterAsync. The tenant exists either way, so there's nothing left to do here.
            return;
        }

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
