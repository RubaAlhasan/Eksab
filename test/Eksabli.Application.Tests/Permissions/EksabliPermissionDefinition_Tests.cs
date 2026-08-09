using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.Permissions;

// Regression coverage for a REAL cross-tenant authorization gap found by live-testing this session
// (see NEXT_SESSION_PROMPT.md): every Host-realm permission (Tenants/Users/Categories/SupportTickets/
// AuditLogs/Billing.ManagePlatform) was defined with no MultiTenancySide restriction at all, so a
// freshly-registered tenant's own Owner role — seeded via the exact same "grant all permissions" path
// ABP uses for the Host "admin" role — genuinely ended up holding platform-wide permissions (view/
// approve/suspend ANY business, the cross-tenant Users directory, platform Billing management, etc.).
// Confirmed via a real registered test tenant's `/api/abp/application-configuration` response before
// the fix (`Eksabli.Tenants.View` etc. all `true`), not simulated.
public abstract class EksabliPermissionDefinition_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IPermissionManager _permissionManager;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;

    protected EksabliPermissionDefinition_Tests()
    {
        _permissionManager = GetRequiredService<IPermissionManager>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private async Task<Guid> CreateTenantAsync()
    {
        Guid tenantId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            var tenant = await _tenantManager.CreateAsync("tenant-" + Guid.NewGuid().ToString("N"));
            await _tenantRepository.InsertAsync(tenant, autoSave: true);
            tenantId = tenant.Id;
        });
        return tenantId;
    }

    [Theory]
    [InlineData(EksabliPermissions.Tenants.View)]
    [InlineData(EksabliPermissions.Tenants.Approve)]
    [InlineData(EksabliPermissions.Tenants.Suspend)]
    [InlineData(EksabliPermissions.Users.View)]
    [InlineData(EksabliPermissions.Categories.Create)]
    [InlineData(EksabliPermissions.SupportTickets.Manage)]
    [InlineData(EksabliPermissions.AuditLogs.Default)]
    [InlineData(EksabliPermissions.Billing.ManagePlatform)]
    public async Task Host_Only_Permission_Should_NOT_Be_Grantable_Inside_A_Tenant(string permissionName)
    {
        var tenantId = await CreateTenantAsync();
        var roleProviderKey = Guid.NewGuid().ToString();

        // "R" is ABP's own well-known RolePermissionValueProvider.ProviderName. A real admin role
        // doesn't need to exist for this — ABP's own PermissionManager.SetAsync actively THROWS
        // (not a silent no-op) the moment the permission's own MultiTenancySide excludes the ambient
        // side, before ever touching a role/provider. Stronger guarantee than a silent no-op would
        // have been — there's no path to a stale/incorrect grant landing in the DB in the first place.
        await Should.ThrowAsync<ApplicationException>(() => WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                await _permissionManager.SetAsync(permissionName, "R", roleProviderKey, isGranted: true);
            }
        }));
    }

    [Fact]
    public async Task Tenant_Side_Permission_Should_Still_Be_Grantable_Inside_A_Tenant()
    {
        var tenantId = await CreateTenantAsync();
        var roleProviderKey = Guid.NewGuid().ToString();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                await _permissionManager.SetAsync(EksabliPermissions.Billing.ManageOwn, "R", roleProviderKey, isGranted: true);

                var result = await _permissionManager.GetAsync(EksabliPermissions.Billing.ManageOwn, "R", roleProviderKey);
                result.IsGranted.ShouldBeTrue("Billing.ManageOwn is a real tenant-side permission and must still work normally.");
            }
        });
    }
}
