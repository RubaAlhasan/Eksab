using System.Linq;
using System.Threading.Tasks;
using Eksabli.EmployeeAssignments;
using Eksabli.Permissions;
using Eksabli.Settings;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;

namespace Eksabli.Data.Seeders;

// Businesses registered before Smart Offers existed got their permissions once, at registration
// (BusinessAppService.GrantOwnerRolePermissionsAsync), and EmployeeRolePermissionDefaults.EnsureTierRoleAsync
// is a no-op for a role that already exists. So no existing tenant ever received the Eksabli.SmartOffers.*
// grants, and its Owner got 403s on every Smart Offers endpoint. This tenant-scoped contributor fills that gap.
// It runs on every DbMigrator pass for each tenant (EksabliDbMigrationService.SeedDataAsync) and during
// registration. A brand-new tenant must NOT be granted here: registration seeds inside its own still-open
// transaction, then grants the Owner's full set (Smart Offers included) in a separate requires-new one
// (BusinessAppService.RegisterAsync). Granting the same AbpPermissionGrants row in the outer transaction
// first makes the inner insert wait on the outer's uncommitted unique key, which in turn waits on the inner
// — on PostgreSQL that hangs until the command timeout. So registration passes NewTenantRegistration and
// this contributor only records the marker.
//
// Runs once per tenant. The marker setting is what keeps it from regranting: without it, an Owner who
// deliberately removed a Smart Offers permission would get it back on the next migrator run. Only the Smart
// Offers permissions are touched, so nothing else on the Owner's or staff roles changes.
//
// A tier role that does not exist yet is skipped. EnsureTierRoleAsync creates it later with the full default
// set, which already includes Smart Offers. Grants go through IPermissionManager.SetForRoleAsync one permission
// at a time, not the bulk seeder, for the same reason as the other per-tenant grant helpers in this codebase.
public class SmartOfferPermissionBackfillDataSeederContributor : IDataSeedContributor, ITransientDependency
{
    public const string NewTenantRegistrationPropertyName = "Eksabli.NewTenantRegistration";

    private const string AdminRoleName = "admin";

    private static readonly string[] SmartOfferPermissions =
    {
        EksabliPermissions.SmartOffers.Default,
        EksabliPermissions.SmartOffers.Create,
        EksabliPermissions.SmartOffers.Edit,
        EksabliPermissions.SmartOffers.Delete,
    };

    private readonly IPermissionManager _permissionManager;
    private readonly IdentityRoleManager _identityRoleManager;
    private readonly ISettingManager _settingManager;
    private readonly ICurrentTenant _currentTenant;

    public SmartOfferPermissionBackfillDataSeederContributor(
        IPermissionManager permissionManager,
        IdentityRoleManager identityRoleManager,
        ISettingManager settingManager,
        ICurrentTenant currentTenant)
    {
        _permissionManager = permissionManager;
        _identityRoleManager = identityRoleManager;
        _settingManager = settingManager;
        _currentTenant = currentTenant;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (context?.TenantId is not { } tenantId)
        {
            return;
        }

        var providerKey = tenantId.ToString();
        var alreadyBackfilled = await _settingManager.GetOrNullAsync(
            EksabliSettings.SmartOffers.PermissionsBackfilled, TenantSettingValueProvider.ProviderName, providerKey);
        if (alreadyBackfilled == "true")
        {
            return;
        }

        if (context[NewTenantRegistrationPropertyName] is true)
        {
            await _settingManager.SetForTenantAsync(tenantId, EksabliSettings.SmartOffers.PermissionsBackfilled, "true");
            return;
        }

        using (_currentTenant.Change(tenantId))
        {
            foreach (var permissionName in SmartOfferPermissions)
            {
                await _permissionManager.SetForRoleAsync(AdminRoleName, permissionName, true);
            }

            foreach (var role in new[] { EmployeeRole.BranchManager, EmployeeRole.MarketingManager })
            {
                var roleName = EmployeeRolePermissionDefaults.RoleName(role);
                if (await _identityRoleManager.FindByNameAsync(roleName) == null)
                {
                    continue;
                }

                var smartOfferDefaults = EmployeeRolePermissionDefaults.DefaultPermissions(role)
                    .Where(permissionName => SmartOfferPermissions.Contains(permissionName));
                foreach (var permissionName in smartOfferDefaults)
                {
                    await _permissionManager.SetForRoleAsync(roleName, permissionName, true);
                }
            }
        }

        await _settingManager.SetForTenantAsync(tenantId, EksabliSettings.SmartOffers.PermissionsBackfilled, "true");
    }
}
