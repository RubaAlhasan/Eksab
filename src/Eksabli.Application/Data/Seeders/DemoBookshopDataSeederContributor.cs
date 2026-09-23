using System;
using System.Threading.Tasks;
using Eksabli.Billing;
using Eksabli.Businesses;
using Eksabli.BusinessProfiles;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace Eksabli.Data.Seeders;

// Second demo business — see DemoBusinessDataSeederContributor's own file comment for the base
// pattern this mirrors exactly (RegisterAsync + immediate approval). StarbucksDemo alone left every
// customer-facing "discover a business you haven't joined yet" / "join a new business" path
// permanently untestable in a fresh dev environment, since a lone seeded business is one every demo
// customer is already a member of by the time they can look. Themed on the prototype's own
// "Cornerstone Bookshop" (prototype/assets/js/demo-data.js, biz_4) rather than an arbitrary name.
[DependsOn(typeof(SubscriptionPlanDataSeederContributor))]
public class DemoBookshopDataSeederContributor : IDataSeedContributor, ITransientDependency
{
    public const string DemoBusinessName = "CornerstoneBookshopDemo";
    public const string DemoOwnerEmail = "owner@cornerstone-demo.eksabli.test";
    public const string DemoOwnerPassword = "1q2w3E*";
    public const string DemoBranchName = "Main Street Branch";

    private const string DuplicateTenantNameErrorCode = "Volo.Abp.TenantManagement:DuplicateTenantName";

    private readonly ITenantRepository _tenantRepository;
    private readonly IBusinessAppService _businessAppService;
    private readonly IRepository<BusinessProfile, Guid> _businessProfileRepository;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentTenant _currentTenant;
    private readonly DefaultLoyaltyProgramDataSeederContributor _defaultLoyaltyProgramDataSeederContributor;

    public DemoBookshopDataSeederContributor(
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
        if (context?.TenantId != null)
        {
            return;
        }

        var existingTenant = await _tenantRepository.FindByNameAsync(DemoBusinessName.Normalize());
        if (existingTenant != null)
        {
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
                DescriptionAr = "مكتبة تجريبية لأغراض الاختبار",
                DescriptionEn = "Demo bookshop for local testing — a second business so join/discover flows have something to join.",
                BranchName = DemoBranchName,
                BranchAddress = "45 Main Street",
                BranchPhone = "+966500000001",
                OwnerEmail = DemoOwnerEmail,
                OwnerPassword = DemoOwnerPassword
            });
        }
        catch (BusinessException ex) when (ex.Code == DuplicateTenantNameErrorCode)
        {
            return;
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var businessProfile = await _businessProfileRepository.GetAsync(result.BusinessProfileId);
            businessProfile.Approve();
            await _businessProfileRepository.UpdateAsync(businessProfile);
        }
    }
}
