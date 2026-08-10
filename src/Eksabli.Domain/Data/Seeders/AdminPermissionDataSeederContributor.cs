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
// permission to that role, so a fresh host admin ends up with zero AbpPermissionGrants rows
// (confirmed by querying the DB directly) and the UI shows almost nothing. This grants the full,
// currently-registered permission set to the host's "admin" role.
//
// Host-only by design (context.TenantId must be null): granting a *tenant's own* admin role its
// full permission set here reproducibly hit AbpPermissionGrants' unique-index violation — a
// different permission name each run — when done from within BusinessAppService.RegisterAsync's
// nested per-tenant IDataSeeder.SeedAsync call (i.e. when provisioning a new business via
// DemoBusinessDataSeederContributor). Root cause not fully pinned down (tried: de-duplicating the
// input list, an in-process per-tenant guard, forcing an immediate flush, granting only root
// permissions and relying on IPermissionDataSeeder's own child cascade — each shifted which specific
// permission collided rather than eliminating it, pointing at a deeper interaction between nested
// tenant-scoped seed passes and this API rather than anything in this class's own list-building).
// Not needed for the mobile customer-facing API (Postman collection) a new tenant's Branch/BusinessProfile
// enable — that never authenticates as the business's "admin" role — so this scopes to host-only
// rather than block business provisioning on an unresolved framework interaction. Revisit if a
// tenant-admin-facing portal ever needs it.
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
        if (context?.TenantId != null)
        {
            return;
        }

        // GetPermissionsAsync() returns duplicate entries for most permissions (each one appears once
        // per group/provider traversal) — Distinct() avoids seeding the same grant row twice.
        var permissionNames = (await _permissionDefinitionManager.GetPermissionsAsync())
            .Select(p => p.Name)
            .Distinct()
            .ToArray();

        await _permissionDataSeeder.SeedAsync(
            RolePermissionValueProvider.ProviderName,
            "admin",
            permissionNames,
            null);
    }
}
