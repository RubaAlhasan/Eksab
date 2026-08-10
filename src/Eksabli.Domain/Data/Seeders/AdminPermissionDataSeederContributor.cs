using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;

namespace Eksabli.Data.Seeders;

// Volo.Abp.Identity.IdentityDataSeedContributor only creates the "admin" role/user — unlike most
// ABP-scaffolded app templates, this solution never got the usual companion step that grants every
// permission to that role, so a fresh admin ends up with zero AbpPermissionGrants rows (confirmed
// by querying the DB directly) and the UI shows almost nothing. This runs after IdentityDataSeedContributor
// and grants the full, currently-registered permission set (including all Eksabli.* permissions) to
// the "admin" role at host level. IPermissionDataSeeder.SeedAsync only inserts what's missing, so
// re-running this — e.g. after adding a new permission — is safe and picks up the new one too.
[DependsOn(typeof(IdentityDataSeedContributor))]
public class AdminPermissionDataSeederContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IPermissionDataSeeder _permissionDataSeeder;
    private readonly IPermissionDefinitionManager _permissionDefinitionManager;

    public AdminPermissionDataSeederContributor(
        IPermissionDataSeeder permissionDataSeeder,
        IPermissionDefinitionManager permissionDefinitionManager)
    {
        _permissionDataSeeder = permissionDataSeeder;
        _permissionDefinitionManager = permissionDefinitionManager;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        // GetPermissionsAsync() returns duplicate entries for most permissions (each one appears once
        // per group/provider traversal) — Distinct() avoids seeding the same grant row twice, since
        // the AbpPermissionGrants unique index doesn't dedupe host-side (TenantId IS NULL) rows.
        var permissionNames = (await _permissionDefinitionManager.GetPermissionsAsync())
            .Select(p => p.Name)
            .Distinct()
            .ToArray();

        await _permissionDataSeeder.SeedAsync(
            RolePermissionValueProvider.ProviderName,
            "admin",
            permissionNames,
            context?.TenantId);
    }
}
