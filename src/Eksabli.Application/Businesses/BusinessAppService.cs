using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Eksabli.Billing;
using Eksabli.BusinessProfiles;
using Eksabli.Branches;
using Eksabli.Data.Seeders;
using Eksabli.EmployeeAssignments;
using Eksabli.Settings;
using Eksabli.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.BlobStoring;
using Volo.Abp.Content;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Settings;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

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
    private readonly IPermissionDefinitionManager _permissionDefinitionManager;
    private readonly IPermissionManager _permissionManager;
    private readonly IdentityRoleManager _identityRoleManager;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

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
        IDataFilter dataFilter,
        IPermissionDefinitionManager permissionDefinitionManager,
        IPermissionManager permissionManager,
        IdentityRoleManager identityRoleManager,
        IUnitOfWorkManager unitOfWorkManager)
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
        _permissionDefinitionManager = permissionDefinitionManager;
        _permissionManager = permissionManager;
        _identityRoleManager = identityRoleManager;
        _unitOfWorkManager = unitOfWorkManager;
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
                    .WithProperty(IdentityDataSeedContributor.AdminPasswordPropertyName, input.OwnerPassword)
                    .WithProperty(SmartOfferPermissionBackfillDataSeederContributor.NewTenantRegistrationPropertyName, true));

            var ownerUser = await _identityUserRepository.FindByNormalizedUserNameAsync("ADMIN")
                ?? throw new AbpException("Tenant admin seeding did not produce the expected user.");
            ownerUserId = ownerUser.Id;

            // Isolated in its own REQUIRES-NEW transaction, as defense in depth — this whole method
            // runs inside DemoBusinessDataSeederContributor's own nested, [UnitOfWork]-wrapped seed
            // pass on every Host startup (see that class's own comment on the recursion), and this
            // permission-granting work failing must never be able to roll back everything ELSE already
            // done in that same outer pass again (confirmed live: it once took OpenIddictDataSeed
            // Contributor's own scope/application registration down with it, vanishing the "Eksabli"
            // API scope from /.well-known/openid-configuration entirely and breaking every login on
            // the platform, not just this feature). The actual root cause of that failure —
            // IPermissionDataSeeder.SeedAsync's bulk-insert throwing AbpPermissionGrants' unique-
            // constraint violation, the exact bug already documented on AdminPermissionDataSeeder
            // Contributor (a different permission name each run) — is fixed below by not using that
            // API at all (see GrantOwnerRolePermissionsAsync's and EnsureTierRoleAsync's own comments);
            // this isolation stays anyway, since anything unexpected here should never be able to
            // undo unrelated, already-committed seeding again.
            using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
            {
                await GrantOwnerRolePermissionsAsync();

                // Eager, not lazy — without this, BranchManager/Cashier/MarketingManager's roles (and
                // their default permissions) only existed once an Owner had actually invited someone
                // into that tier, so the Roles & Permissions page couldn't show — or let the Owner
                // adjust — a tier's permissions before ever using it. EmployeeAssignmentAppService still
                // calls the same shared helper too, as a safety net for a tenant registered before this
                // existed.
                foreach (var role in new[] { EmployeeRole.BranchManager, EmployeeRole.Cashier, EmployeeRole.MarketingManager })
                {
                    await EmployeeRolePermissionDefaults.EnsureTierRoleAsync(
                        role, _identityRoleManager, _permissionManager, GuidGenerator.Create(), tenant.Id);
                }

                await uow.CompleteAsync();
            }

            var businessProfile = BusinessProfile.Create(GuidGenerator.Create(), input.CategoryId);
            // Set explicitly at creation rather than left null to rely on the Tenant.Name fallback
            // every reader applies (see BusinessProfile.DisplayName's own comment) — makes the row's
            // own data complete from day one instead of depending on a join for every fresh
            // registration. Prefers the caller's own DisplayName when given (e.g. the admin create
            // form lets a platform admin pick a different customer-facing name up front); falls back
            // to BusinessName, so both start out identical for any caller that never supplies one.
            businessProfile.SetDisplayName(
                string.IsNullOrWhiteSpace(input.DisplayName) ? input.BusinessName : input.DisplayName);
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

            await ProvisionTrialSubscriptionAsync(tenant.Id, input.Currency);
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

    // Without this, the tenant's own "admin" role (just created above by IdentityDataSeedContributor)
    // had zero permission grants of any kind — every Eksabli.* permission-gated Business Portal
    // endpoint was unreachable to the Owner themselves, and there was no way to reach the Roles &
    // Permissions page at all (AbpIdentity.Roles.* ungranted too). Same "grant everything currently
    // defined" shape as AdminPermissionDataSeederContributor (Host's own "admin" role), but scoped to
    // just this tenant's real permissions — MultiTenancySides.Tenant/.Both only (Host-only permissions
    // like Tenants.Approve/Users.View/AuditLogs are explicitly excluded via
    // EksabliPermissionDefinitionProvider's own MultiTenancySides restrictions, closing the exact
    // cross-tenant leak that class's own comment already documents from an earlier attempt at this).
    // Runs inside the caller's _currentTenant.Change(tenant.Id) block, same shape as the other
    // per-tenant provisioning here — deliberately NOT reused for already-existing tenants (this only
    // runs at registration time, once).
    private async Task GrantOwnerRolePermissionsAsync()
    {
        var tenantPermissionNames = (await _permissionDefinitionManager.GetPermissionsAsync())
            .Where(p => p.Name.StartsWith("Eksabli.", StringComparison.Ordinal) && p.MultiTenancySide != MultiTenancySides.Host)
            .Select(p => p.Name)
            .Distinct()
            .ToList();

        // The ABP Identity module's own Roles management (list/create/edit + the "Permissions" modal
        // that actually grants/revokes) — needed for the Owner to use the Roles & Permissions page at
        // all, not just to hold Eksabli.* permissions themselves.
        //
        // Users deliberately gets a NARROWER set — view + individual permission overrides only, NOT
        // Create/Delete/Update/Update.ManageRoles. Creating or deleting a staff login, and changing
        // who's in which role, must stay exclusively on the Employees page
        // (EmployeeAssignmentAppService), which keeps EmployeeAssignment.Role and the person's real
        // ABP role membership in sync together — doing either from the stock Users page instead would
        // create a second, unsynced path to the same thing (confirmed as a real gap: without this
        // restriction, the Owner could create a login here with no EmployeeAssignment at all, or move
        // someone to a role the Employees page's own tier tracking never finds out about).
        tenantPermissionNames.AddRange(new[]
        {
            "AbpIdentity.Roles",
            "AbpIdentity.Roles.Create",
            "AbpIdentity.Roles.Update",
            "AbpIdentity.Roles.Delete",
            "AbpIdentity.Roles.ManagePermissions",
            "AbpIdentity.Users",
            "AbpIdentity.Users.ManagePermissions",
        });

        // One call per permission (IPermissionManager.SetForRoleAsync), NOT IPermissionDataSeeder
        // .SeedAsync's bulk-insert shape — see EmployeeRolePermissionDefaults.EnsureTierRoleAsync's
        // own comment for why: that bulk API reproducibly threw AbpPermissionGrants' unique-constraint
        // violation on a list this shaped (a parent permission alongside its own children), which,
        // running inside this method's caller's shared seed-pass transaction, once rolled back
        // unrelated, already-committed seeding too (see this method's own call site for the full
        // story). SetForRoleAsync is a genuine idempotent upsert, safe to call however many times.
        foreach (var permissionName in tenantPermissionNames)
        {
            await _permissionManager.SetForRoleAsync("admin", permissionName, true);
        }
    }

    // Trial, not permanent freemium — see docs/eksabli-loyalty-platform/01-business-strategy.md#revenue-model--pricing.
    // Runs inside the caller's _currentTenant.Change(tenant.Id) block, same shape as the other
    // per-tenant provisioning above (BusinessProfile/Branch/EmployeeAssignment).
    private async Task ProvisionTrialSubscriptionAsync(Guid tenantId, Currency currency)
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
            TenantSubscriptionStatus.Trialing,
            currency);
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
        profile.SetDisplayName(input.DisplayName);
        profile.SetDescription(input.DescriptionAr, input.DescriptionEn);
        profile.SetWebsite(input.Website);
        profile.SetSocialLinks(input.SocialLinksJson);
        // Null keeps the current zone rather than clearing it: the zone is never meant to be empty.
        if (!string.IsNullOrWhiteSpace(input.TimeZoneId))
        {
            profile.SetTimeZone(input.TimeZoneId);
        }
        // Not "keep if null" like the zone: null here means "never expire", and that is a real choice a business can make.
        profile.SetPointsExpiryMonths(input.PointsExpiryMonths);

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
