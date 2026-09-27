using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;

namespace Eksabli.Data.Seeders;

// Volo.Abp.Identity.IdentityDataSeedContributor only creates the "admin" role/user — unlike most
// ABP-scaffolded app templates, this solution never got the usual companion step that grants every
// permission to that role, so a fresh host admin ends up with zero AbpPermissionGrants rows
// (confirmed by querying the DB directly) and the UI shows almost nothing. This grants the full,
// currently-registered permission set to the host's "admin" role.
//
// Host-only by design (context.TenantId must be null) — kept that way even after the fix below,
// since a tenant's own admin role gets its own, narrower permission set from
// BusinessAppService.GrantOwnerRolePermissionsAsync at registration time instead (Host-only
// permissions like Tenants.Approve/Users.View/AuditLogs are excluded there via
// EksabliPermissionDefinitionProvider's own MultiTenancySides restrictions — granting this class's
// full, unfiltered set to a tenant would leak those in).
//
// Previously used IPermissionDataSeeder.SeedAsync for one bulk insert of the whole (200+ entry)
// permission list. That reproducibly threw AbpPermissionGrants' unique-index violation — a different
// permission name each run — confirmed live on a freshly created (empty) database, where this runs
// as one large batch instead of a small/no-op one; a previous investigation (de-duplicating the
// input, forcing an immediate flush, granting only root permissions and relying on the seeder's own
// child cascade) shifted which permission collided without eliminating it. Root cause: the bulk API
// itself, not this class's own list-building — see BusinessAppService.GrantOwnerRolePermissionsAsync
// and EmployeeRolePermissionDefaults.EnsureTierRoleAsync, which hit the exact same failure and were
// fixed the same way. IPermissionManager.SetForRoleAsync (one call per permission) is a genuine
// idempotent upsert with no batch-insert step to collide.
//
// A second, framework-level instance of the exact same bug was found later: Volo.Abp.PermissionManagement
// .PermissionDataSeedContributor (registered in AbpDataSeedOptions.Contributors, not something this
// app calls directly) does its own "grant everything" pass using that same buggy bulk API, and runs
// automatically on every generic IDataSeeder.SeedAsync(...) call — including
// BusinessAppService.RegisterAsync's tenant-scoped one. That is disabled in EksabliDomainModule
// (DisableFrameworkPermissionDataSeedContributor) rather than here, since it is unrelated to this
// class's own Host-side pass.
[DependsOn(typeof(IdentityDataSeedContributor))]
public class AdminPermissionDataSeederContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IPermissionManager _permissionManager;
    private readonly IPermissionDefinitionManager _permissionDefinitionManager;

    public AdminPermissionDataSeederContributor(
        IPermissionManager permissionManager,
        IPermissionDefinitionManager permissionDefinitionManager)
    {
        _permissionManager = permissionManager;
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
        //
        // Some permissions (e.g. AbpIdentity.UserLookup) restrict which providers can hold a grant of
        // them at all via PermissionDefinition.Providers — an empty list means "any provider", a
        // non-empty one is an allow-list (confirmed live: PermissionManager.SetAsync throws
        // ApplicationException "not compatible with the provider named 'R'" for one of these when
        // granted to a role). Skip anything that doesn't allow "R" (Role) rather than trying to grant
        // every permission in the system unconditionally.
        //
        // Likewise, most Eksabli.* permissions are explicitly MultiTenancySides.Tenant-only (see
        // EksabliPermissionDefinitionProvider) — a Host-side role structurally cannot hold one
        // (confirmed live: SetAsync throws ApplicationException "has multitenancy side 'Tenant' which
        // is not compatible with the current multitenancy side 'Host'" for e.g.
        // Eksabli.BusinessProfile). This method only ever runs Host-side (see the TenantId guard
        // above), so skip anything that isn't compatible with MultiTenancySides.Host.
        var permissionNames = (await _permissionDefinitionManager.GetPermissionsAsync())
            .Where(p => p.Providers.Count == 0 || p.Providers.Contains("R"))
            .Where(p => p.MultiTenancySide.HasFlag(MultiTenancySides.Host))
            .Select(p => p.Name)
            .Distinct();

        foreach (var permissionName in permissionNames)
        {
            await _permissionManager.SetForRoleAsync("admin", permissionName, true);
        }
    }
}
