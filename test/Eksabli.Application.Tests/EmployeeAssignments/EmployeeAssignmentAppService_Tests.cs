using System;
using System.Threading.Tasks;
using Eksabli.Businesses;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Eksabli.EmployeeAssignments;

public abstract class EmployeeAssignmentAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IEmployeeAssignmentAppService _employeeAssignmentAppService;
    private readonly IBusinessAppService _businessAppService;
    private readonly ICurrentTenant _currentTenant;

    protected EmployeeAssignmentAppService_Tests()
    {
        _employeeAssignmentAppService = GetRequiredService<IEmployeeAssignmentAppService>();
        _businessAppService = GetRequiredService<IBusinessAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    // These tests used to call InviteAsync with no tenant at all, which is not a scenario that can
    // happen in the product: inviting staff is a Business Portal action, so the caller always
    // carries a tenant. Host-side, InviteAsync hands CurrentTenant.Id == null down to
    // EmployeeRolePermissionDefaults.EnsureTierRoleAsync, and granting a MultiTenancySides.Tenant
    // permission (every one in that tier's set) then fails with
    //
    //   The permission named 'Eksabli.Memberships' has multitenancy side 'Tenant' which is not
    //   compatible with the current multitenancy side 'Host'
    //
    // Registering a business gives the test a real tenant to invite inside, which is both what the
    // product does and the only way the permission grants can succeed.
    private async Task<Guid> CreateTenantAsync()
    {
        var result = await WithUnitOfWorkAsync(() => _businessAppService.RegisterAsync(new RegisterBusinessDto
        {
            BusinessName = "Employee Tests " + Guid.NewGuid().ToString("N"),
            BranchName = "Main Branch",
            BranchAddress = "123 Street",
            OwnerEmail = $"owner-{Guid.NewGuid():N}@example.com",
            OwnerPassword = "1q2w3E*"
        }));

        return result.TenantId;
    }

    [Fact]
    public async Task Should_Invite_New_Employee_And_List_It()
    {
        var tenantId = await CreateTenantAsync();
        var email = $"cashier-{Guid.NewGuid():N}@example.com";

        using (_currentTenant.Change(tenantId))
        {
            var invited = await WithUnitOfWorkAsync(() => _employeeAssignmentAppService.InviteAsync(new InviteEmployeeDto
            {
                Email = email,
                Role = EmployeeRole.Cashier
            }));

            invited.Assignment.UserEmail.ShouldBe(email);
            invited.Assignment.Role.ShouldBe(EmployeeRole.Cashier);
            // The only place this password is ever surfaced — no email is sent anywhere in this codebase,
            // so InviteAsync's response is the sole hand-off point (see EmployeeAssignmentAppService's own
            // comment on this).
            invited.TemporaryPassword.ShouldNotBeNullOrWhiteSpace();

            var list = await WithUnitOfWorkAsync(() => _employeeAssignmentAppService.GetListAsync(new PagedAndSortedResultRequestDto()));
            list.Items.ShouldContain(x => x.Id == invited.Assignment.Id);
        }
    }

    [Fact]
    public async Task Should_Not_Invite_The_Same_Email_Twice()
    {
        var tenantId = await CreateTenantAsync();
        var email = $"manager-{Guid.NewGuid():N}@example.com";

        using (_currentTenant.Change(tenantId))
        {
            await WithUnitOfWorkAsync(() => _employeeAssignmentAppService.InviteAsync(new InviteEmployeeDto
            {
                Email = email,
                Role = EmployeeRole.BranchManager
            }));

            await Assert.ThrowsAsync<UserFriendlyException>(async () =>
            {
                await WithUnitOfWorkAsync(() => _employeeAssignmentAppService.InviteAsync(new InviteEmployeeDto
                {
                    Email = email,
                    Role = EmployeeRole.BranchManager
                }));
            });
        }
    }
}
