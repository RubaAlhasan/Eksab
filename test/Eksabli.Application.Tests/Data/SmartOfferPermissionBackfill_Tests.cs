using System;
using System.Threading.Tasks;
using Eksabli.Businesses;
using Eksabli.EmployeeAssignments;
using Eksabli.Permissions;
using Eksabli.Settings;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;
using Xunit;

namespace Eksabli.Data.Seeders;

public abstract class SmartOfferPermissionBackfill_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string OwnerRoleName = "admin";

    private readonly IBusinessAppService _businessAppService;
    private readonly IPermissionManager _permissionManager;
    private readonly ISettingManager _settingManager;
    private readonly SmartOfferPermissionBackfillDataSeederContributor _backfill;
    private readonly ICurrentTenant _currentTenant;

    protected SmartOfferPermissionBackfill_Tests()
    {
        _businessAppService = GetRequiredService<IBusinessAppService>();
        _permissionManager = GetRequiredService<IPermissionManager>();
        _settingManager = GetRequiredService<ISettingManager>();
        _backfill = GetRequiredService<SmartOfferPermissionBackfillDataSeederContributor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private static RegisterBusinessDto CreateInput(string businessName) => new RegisterBusinessDto
    {
        BusinessName = businessName,
        BranchName = "Main Branch",
        BranchAddress = "123 Street",
        OwnerEmail = $"owner-{Guid.NewGuid():N}@example.com",
        OwnerPassword = "1q2w3E*"
    };

    private static readonly string[] SmartOfferPermissions =
    {
        EksabliPermissions.SmartOffers.Default,
        EksabliPermissions.SmartOffers.Create,
        EksabliPermissions.SmartOffers.Edit,
        EksabliPermissions.SmartOffers.Delete,
    };

    private async Task<bool> IsGrantedAsync(string roleName, string permissionName) =>
        (await _permissionManager.GetAsync(permissionName, "R", roleName)).IsGranted;

    // Mirrors a business registered before Smart Offers existed: its roles have no Smart Offers grants and
    // the backfill has never run for it.
    private async Task SimulateLegacyTenantAsync(Guid tenantId, params string[] roleNames)
    {
        using (_currentTenant.Change(tenantId))
        {
            foreach (var roleName in roleNames)
            {
                foreach (var permissionName in SmartOfferPermissions)
                {
                    await _permissionManager.SetForRoleAsync(roleName, permissionName, false);
                }
            }
        }

        await _settingManager.SetForTenantAsync(tenantId, EksabliSettings.SmartOffers.PermissionsBackfilled, "false");
    }

    [Fact]
    public async Task Should_Grant_Smart_Offer_Permissions_To_Owner_And_Existing_Tier_Roles_For_A_Legacy_Tenant()
    {
        var result = await WithUnitOfWorkAsync(() =>
            _businessAppService.RegisterAsync(CreateInput("Backfill Biz " + Guid.NewGuid().ToString("N"))));

        await WithUnitOfWorkAsync(() => SimulateLegacyTenantAsync(
            result.TenantId, OwnerRoleName, EmployeeRolePermissionDefaults.RoleName(EmployeeRole.BranchManager),
            EmployeeRolePermissionDefaults.RoleName(EmployeeRole.MarketingManager)));

        await WithUnitOfWorkAsync(() => _backfill.SeedAsync(new DataSeedContext(result.TenantId)));

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(result.TenantId))
            {
                foreach (var permissionName in SmartOfferPermissions)
                {
                    (await IsGrantedAsync(OwnerRoleName, permissionName)).ShouldBeTrue();
                    (await IsGrantedAsync(EmployeeRolePermissionDefaults.RoleName(EmployeeRole.BranchManager), permissionName)).ShouldBeTrue();
                    (await IsGrantedAsync(EmployeeRolePermissionDefaults.RoleName(EmployeeRole.MarketingManager), permissionName)).ShouldBeTrue();
                }
            }
        });
    }

    [Fact]
    public async Task Should_Not_Regrant_Smart_Offer_Permissions_Once_The_Backfill_Has_Run()
    {
        var result = await WithUnitOfWorkAsync(() =>
            _businessAppService.RegisterAsync(CreateInput("Backfill Once Biz " + Guid.NewGuid().ToString("N"))));

        await WithUnitOfWorkAsync(() => _backfill.SeedAsync(new DataSeedContext(result.TenantId)));

        // The Owner deliberately removes a Smart Offers permission after the backfill has already run.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(result.TenantId))
            {
                await _permissionManager.SetForRoleAsync(OwnerRoleName, EksabliPermissions.SmartOffers.Delete, false);
            }
        });

        await WithUnitOfWorkAsync(() => _backfill.SeedAsync(new DataSeedContext(result.TenantId)));

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(result.TenantId))
            {
                (await IsGrantedAsync(OwnerRoleName, EksabliPermissions.SmartOffers.Delete)).ShouldBeFalse();
            }
        });
    }
}
