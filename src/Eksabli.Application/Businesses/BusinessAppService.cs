using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Eksabli.Billing;
using Eksabli.BusinessProfiles;
using Eksabli.Branches;
using Eksabli.EmployeeAssignments;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace Eksabli.Businesses;

[RemoteService(IsEnabled = false)]
public class BusinessAppService : ApplicationService, IBusinessAppService
{
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly IDataSeeder _dataSeeder;
    private readonly ICurrentTenant _currentTenant;
    private readonly IRepository<BusinessProfile, Guid> _businessProfileRepository;
    private readonly IRepository<Branch, Guid> _branchRepository;
    private readonly IRepository<EmployeeAssignment, Guid> _employeeAssignmentRepository;
    private readonly ITenantSubscriptionRepository _tenantSubscriptionRepository;
    private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;
    private readonly IFeatureManager _featureManager;

    public BusinessAppService(
        TenantManager tenantManager,
        ITenantRepository tenantRepository,
        IIdentityUserRepository identityUserRepository,
        IDataSeeder dataSeeder,
        ICurrentTenant currentTenant,
        IRepository<BusinessProfile, Guid> businessProfileRepository,
        IRepository<Branch, Guid> branchRepository,
        IRepository<EmployeeAssignment, Guid> employeeAssignmentRepository,
        ITenantSubscriptionRepository tenantSubscriptionRepository,
        ISubscriptionPlanRepository subscriptionPlanRepository,
        IFeatureManager featureManager)
    {
        _tenantManager = tenantManager;
        _tenantRepository = tenantRepository;
        _identityUserRepository = identityUserRepository;
        _dataSeeder = dataSeeder;
        _currentTenant = currentTenant;
        _businessProfileRepository = businessProfileRepository;
        _branchRepository = branchRepository;
        _employeeAssignmentRepository = employeeAssignmentRepository;
        _tenantSubscriptionRepository = tenantSubscriptionRepository;
        _subscriptionPlanRepository = subscriptionPlanRepository;
        _featureManager = featureManager;
    }

    public async Task<BusinessRegistrationResultDto> RegisterAsync(RegisterBusinessDto input)
    {
        // TenantManager.CreateAsync already validates name uniqueness (throws BusinessException) —
        // no need to duplicate that check here.
        var tenant = await _tenantManager.CreateAsync(input.BusinessName);
        await _tenantRepository.InsertAsync(tenant, autoSave: true);

        Guid businessProfileId, branchId, ownerUserId;

        using (_currentTenant.Change(tenant.Id))
        {
            await _dataSeeder.SeedAsync(
                new DataSeedContext(tenant.Id)
                    .WithProperty(IdentityDataSeedContributor.AdminEmailPropertyName, input.OwnerEmail)
                    .WithProperty(IdentityDataSeedContributor.AdminPasswordPropertyName, input.OwnerPassword));

            var ownerUser = await _identityUserRepository.FindByNormalizedUserNameAsync("ADMIN")
                ?? throw new AbpException("Tenant admin seeding did not produce the expected user.");
            ownerUserId = ownerUser.Id;

            var businessProfile = BusinessProfile.Create(GuidGenerator.Create(), input.CategoryId);
            businessProfile.SetDescription(input.DescriptionAr, input.DescriptionEn);
            businessProfile.SetWebsite(input.Website);
            await _businessProfileRepository.InsertAsync(businessProfile, autoSave: true);
            businessProfileId = businessProfile.Id;

            var branch = Branch.Create(GuidGenerator.Create(), input.BranchName);
            branch.SetAddress(input.BranchAddress);
            branch.SetPhone(input.BranchPhone);
            await _branchRepository.InsertAsync(branch, autoSave: true);
            branchId = branch.Id;

            var employeeAssignment = EmployeeAssignment.Create(GuidGenerator.Create(), ownerUserId, EmployeeRole.Owner);
            await _employeeAssignmentRepository.InsertAsync(employeeAssignment, autoSave: true);

            await ProvisionTrialSubscriptionAsync(tenant.Id);
        }

        return new BusinessRegistrationResultDto
        {
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            BusinessProfileId = businessProfileId,
            BranchId = branchId,
            OwnerUserId = ownerUserId
        };
    }

    // Trial, not permanent freemium — see docs/eksabli-loyalty-platform/01-business-strategy.md#revenue-model--pricing.
    // Runs inside the caller's _currentTenant.Change(tenant.Id) block, same shape as the other
    // per-tenant provisioning above (BusinessProfile/Branch/EmployeeAssignment).
    private async Task ProvisionTrialSubscriptionAsync(Guid tenantId)
    {
        var trialPlan = await _subscriptionPlanRepository.FirstOrDefaultAsync(p => p.IsTrialDefault)
            ?? throw new AbpException("No subscription plan is flagged as the trial default.");

        var subscription = TenantSubscription.Create(
            GuidGenerator.Create(),
            trialPlan.Id,
            Clock.Now,
            Clock.Now.AddDays(BillingConsts.TrialDurationDays),
            TenantSubscriptionStatus.Trialing);
        await _tenantSubscriptionRepository.InsertAsync(subscription, autoSave: true);

        var limits = JsonSerializer.Deserialize<Dictionary<string, string>>(trialPlan.FeatureLimitsJson) ?? new Dictionary<string, string>();
        foreach (var (key, value) in limits)
        {
            await _featureManager.SetForTenantAsync(tenantId, key, value);
        }
    }

    public async Task<BusinessProfileDto> GetProfileAsync()
    {
        var profile = await _businessProfileRepository.SingleAsync();
        return ObjectMapper.Map<BusinessProfile, BusinessProfileDto>(profile);
    }

    public async Task<BusinessProfileDto> UpdateProfileAsync(UpdateBusinessProfileDto input)
    {
        var profile = await _businessProfileRepository.SingleAsync();
        profile.SetCategory(input.CategoryId);
        profile.SetDescription(input.DescriptionAr, input.DescriptionEn);
        profile.SetWebsite(input.Website);
        profile.SetSocialLinks(input.SocialLinksJson);
        await _businessProfileRepository.UpdateAsync(profile);
        return ObjectMapper.Map<BusinessProfile, BusinessProfileDto>(profile);
    }
}
