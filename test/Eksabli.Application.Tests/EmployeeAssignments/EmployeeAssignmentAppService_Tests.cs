using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Modularity;
using Xunit;

namespace Eksabli.EmployeeAssignments;

public abstract class EmployeeAssignmentAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IEmployeeAssignmentAppService _employeeAssignmentAppService;

    protected EmployeeAssignmentAppService_Tests()
    {
        _employeeAssignmentAppService = GetRequiredService<IEmployeeAssignmentAppService>();
    }

    [Fact]
    public async Task Should_Invite_New_Employee_And_List_It()
    {
        var email = $"cashier-{Guid.NewGuid():N}@example.com";

        var invited = await WithUnitOfWorkAsync(() => _employeeAssignmentAppService.InviteAsync(new InviteEmployeeDto
        {
            Email = email,
            Role = EmployeeRole.Cashier
        }));

        invited.UserEmail.ShouldBe(email);
        invited.Role.ShouldBe(EmployeeRole.Cashier);

        var list = await WithUnitOfWorkAsync(() => _employeeAssignmentAppService.GetListAsync(new PagedAndSortedResultRequestDto()));
        list.Items.ShouldContain(x => x.Id == invited.Id);
    }

    [Fact]
    public async Task Should_Not_Invite_The_Same_Email_Twice()
    {
        var email = $"manager-{Guid.NewGuid():N}@example.com";

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
