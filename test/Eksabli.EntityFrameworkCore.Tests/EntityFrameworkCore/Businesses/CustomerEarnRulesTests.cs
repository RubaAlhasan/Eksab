using System;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.Branches;
using Eksabli.BusinessProfiles;
using Eksabli.Businesses;
using Eksabli.Shared;
using Eksabli.Wallets;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Businesses;

public class CustomerEarnRulesTests : EksabliEntityFrameworkCoreTestBase
{
    private readonly ICustomerBusinessAppService _customerBusinessService;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<BusinessProfile, Guid> _profileRepository;
    private readonly IRepository<PointRule, Guid> _pointRuleRepository;
    private readonly IRepository<Branch, Guid> _branchRepository;
    private readonly ICurrentTenant _currentTenant;

    public CustomerEarnRulesTests()
    {
        _customerBusinessService = GetRequiredService<ICustomerBusinessAppService>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _profileRepository = GetRequiredService<IRepository<BusinessProfile, Guid>>();
        _pointRuleRepository = GetRequiredService<IRepository<PointRule, Guid>>();
        _branchRepository = GetRequiredService<IRepository<Branch, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private async Task<Guid> CreateBusinessAsync(bool approved)
    {
        var tenantId = Guid.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            var tenant = await _tenantManager.CreateAsync("tenant-" + Guid.NewGuid().ToString("N"));
            await _tenantRepository.InsertAsync(tenant, autoSave: true);
            tenantId = tenant.Id;

            using (_currentTenant.Change(tenantId))
            {
                var profile = BusinessProfile.Create(Guid.NewGuid());
                if (approved)
                {
                    profile.Approve();
                }
                await _profileRepository.InsertAsync(profile, autoSave: true);

                await _pointRuleRepository.InsertAsync(PointRule.Create(Guid.NewGuid(), PointRuleType.PerCurrencyUnit, 2m, Currency.Usd), autoSave: true);
                await _pointRuleRepository.InsertAsync(PointRule.Create(Guid.NewGuid(), PointRuleType.PerVisit, 5m), autoSave: true);
            }
        });
        return tenantId;
    }

    [Fact]
    public async Task An_approved_business_publishes_its_earn_rules_to_customers()
    {
        var tenantId = await CreateBusinessAsync(approved: true);

        var rules = await _customerBusinessService.GetEarnRulesAsync(tenantId);

        rules.Count.ShouldBe(2);
        rules.Single(r => r.RuleType == PointRuleType.PerCurrencyUnit).Currency.ShouldBe(Currency.Usd);
        rules.Single(r => r.RuleType == PointRuleType.PerCurrencyUnit).PointsPerUnit.ShouldBe(2m);
        rules.Single(r => r.RuleType == PointRuleType.PerVisit).PointsPerUnit.ShouldBe(5m);
    }

    [Fact]
    public async Task A_business_that_is_not_approved_publishes_no_earn_rules()
    {
        var tenantId = await CreateBusinessAsync(approved: false);

        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(() => _customerBusinessService.GetEarnRulesAsync(tenantId));
    }

    [Fact]
    public async Task A_branch_publishes_its_address_hours_and_map_position_to_customers()
    {
        var tenantId = await CreateBusinessAsync(approved: true);
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var branch = Branch.Create(Guid.NewGuid(), "Downtown");
                branch.SetAddress("Main street 1");
                branch.SetLocation(33.5, 36.3);
                branch.SetPhone("+963 11 000");
                branch.SetOpeningHours("Sat-Thu 09:00-22:00");
                await _branchRepository.InsertAsync(branch, autoSave: true);
            }
        });

        var business = await _customerBusinessService.GetAsync(tenantId);

        var listed = business.Branches.Single();
        listed.Address.ShouldBe("Main street 1");
        listed.Latitude.ShouldBe(33.5);
        listed.Longitude.ShouldBe(36.3);
        listed.OpeningHours.ShouldBe("Sat-Thu 09:00-22:00");
    }
}
