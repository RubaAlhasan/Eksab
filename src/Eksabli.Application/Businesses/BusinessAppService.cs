using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Eksabli.Billing;
using Eksabli.BusinessProfiles;
using Eksabli.Branches;
using Eksabli.EmployeeAssignments;
using Eksabli.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.Content;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
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
    private readonly ISettingProvider _settingProvider;
    private readonly IBlobContainer<BusinessLogoContainer> _logoContainer;
    private readonly IDataFilter _dataFilter;

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
        IFeatureManager featureManager,
        ISettingProvider settingProvider,
        IBlobContainer<BusinessLogoContainer> logoContainer,
        IDataFilter dataFilter)
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
        _settingProvider = settingProvider;
        _logoContainer = logoContainer;
        _dataFilter = dataFilter;
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
            businessProfile.SetSocialLinks(BuildSocialLinksJson(input.InstagramUrl, input.FacebookUrl));
            await _businessProfileRepository.InsertAsync(businessProfile, autoSave: true);
            businessProfileId = businessProfile.Id;

            var branch = Branch.Create(GuidGenerator.Create(), input.BranchName);
            branch.SetAddress(input.BranchAddress);
            branch.SetPhone(input.BranchPhone);
            branch.SetLocation(input.BranchLatitude, input.BranchLongitude);
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

        // Configurable via Setting Management (Eksabli.Trial.LengthDays) rather than the fixed
        // BillingConsts.TrialDurationDays constant — falls back to that constant's value as the
        // setting's own default, so behavior is unchanged until someone actually edits it.
        var trialLengthDays = await _settingProvider.GetAsync(EksabliSettings.Trial.LengthDays, BillingConsts.TrialDurationDays);

        var subscription = TenantSubscription.Create(
            GuidGenerator.Create(),
            trialPlan.Id,
            Clock.Now,
            Clock.Now.AddDays(trialLengthDays),
            TenantSubscriptionStatus.Trialing);
        await _tenantSubscriptionRepository.InsertAsync(subscription, autoSave: true);

        var limits = SubscriptionPlanFeatureLimits.Parse(trialPlan.FeatureLimitsJson);
        foreach (var (key, value) in limits)
        {
            await _featureManager.SetForTenantAsync(tenantId, key, value);
        }
    }

    // BusinessProfile.SocialLinksJson has no fixed schema anywhere else in the codebase (confirmed —
    // it's a freeform blob, same treatment as SubscriptionPlan.FeatureLimitsJson); "instagram"/
    // "facebook" here are just the two keys this endpoint happens to populate, not a schema the
    // column itself enforces. Returns null (not "{}") when neither is provided, matching Website's
    // own null-when-absent shape rather than storing an empty object.
    private static string? BuildSocialLinksJson(string? instagramUrl, string? facebookUrl)
    {
        var links = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(instagramUrl)) links["instagram"] = instagramUrl;
        if (!string.IsNullOrWhiteSpace(facebookUrl)) links["facebook"] = facebookUrl;
        return links.Count > 0 ? JsonSerializer.Serialize(links) : null;
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

    public async Task<BusinessProfileDto> UploadLogoAsync(IRemoteStreamContent file)
    {
        var contentType = file.ContentType?.ToLowerInvariant();
        if (contentType.IsNullOrWhiteSpace() || Array.IndexOf(BusinessProfileConsts.AllowedLogoContentTypes, contentType) < 0)
        {
            throw new UserFriendlyException("Only PNG, JPEG, or WebP images are allowed for the business logo.");
        }

        var profile = await _businessProfileRepository.SingleAsync();

        // ContentLength is caller-supplied and not to be trusted as an enforcement mechanism — the real
        // cap is enforced while reading the stream itself, below.
        using var content = await ReadBoundedAsync(file.GetStream(), BusinessProfileConsts.MaxLogoFileSizeBytes);

        var blobName = $"{CurrentTenant.Id}/{GuidGenerator.Create():N}";
        await _logoContainer.SaveAsync(blobName, content);

        var oldBlobName = profile.LogoBlobName;
        profile.SetLogo(blobName, contentType);
        await _businessProfileRepository.UpdateAsync(profile);

        if (!oldBlobName.IsNullOrWhiteSpace())
        {
            // Best-effort — an orphaned old blob costs storage, not correctness, and the new logo is
            // already saved and already the one the entity references, so a delete failure here
            // shouldn't fail the whole upload.
            await _logoContainer.DeleteAsync(oldBlobName!);
        }

        return ObjectMapper.Map<BusinessProfile, BusinessProfileDto>(profile);
    }

    public async Task<BusinessProfileDto> RemoveLogoAsync()
    {
        var profile = await _businessProfileRepository.SingleAsync();

        if (!profile.LogoBlobName.IsNullOrWhiteSpace())
        {
            await _logoContainer.DeleteAsync(profile.LogoBlobName!);
        }

        profile.SetLogo(null, null);
        await _businessProfileRepository.UpdateAsync(profile);
        return ObjectMapper.Map<BusinessProfile, BusinessProfileDto>(profile);
    }

    // Anonymous read (see IBusinessAppService.GetLogoAsync) — looks up by id directly rather than
    // CurrentTenant, since an anonymous caller has no tenant context to resolve from. IMultiTenant's
    // filter still applies to BusinessProfile even for a Disable<IMultiTenant>() Host-context call
    // reading a specific tenant's row by id, same pattern AdminPlatformReportAppService already uses.
    public async Task<IRemoteStreamContent> GetLogoAsync(Guid businessProfileId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var profile = await _businessProfileRepository.GetAsync(businessProfileId);
            if (profile.LogoBlobName.IsNullOrWhiteSpace())
            {
                throw new EntityNotFoundException(typeof(BusinessProfile), businessProfileId);
            }

            var stream = await _logoContainer.GetAsync(profile.LogoBlobName!);
            return new RemoteStreamContent(stream, "logo", profile.LogoContentType ?? "application/octet-stream");
        }
    }

    private static async Task<MemoryStream> ReadBoundedAsync(Stream source, int maxBytes)
    {
        var buffer = new byte[81920];
        var destination = new MemoryStream();
        int read;
        while ((read = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            if (destination.Length + read > maxBytes)
            {
                throw new UserFriendlyException($"The logo file is too large. Maximum size is {maxBytes / 1024 / 1024} MB.");
            }
            await destination.WriteAsync(buffer, 0, read);
        }
        destination.Seek(0, SeekOrigin.Begin);
        return destination;
    }
}
