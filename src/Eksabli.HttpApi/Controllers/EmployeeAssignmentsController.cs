using System;
using System.Threading.Tasks;
using Eksabli.EmployeeAssignments;
using Eksabli.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/employee-assignments")]
[Authorize(EksabliPermissions.EmployeeAssignments.Default)]
public class EmployeeAssignmentsController : EksabliController
{
    private readonly IEmployeeAssignmentAppService _employeeAssignmentAppService;

    public EmployeeAssignmentsController(IEmployeeAssignmentAppService employeeAssignmentAppService)
    {
        _employeeAssignmentAppService = employeeAssignmentAppService;
    }

    [HttpGet]
    public Task<PagedResultDto<EmployeeAssignmentDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _employeeAssignmentAppService.GetListAsync(input);
    }

    [Authorize(EksabliPermissions.EmployeeAssignments.Create)]
    [HttpPost("invite")]
    public Task<InviteEmployeeResultDto> InviteAsync(InviteEmployeeDto input)
    {
        return _employeeAssignmentAppService.InviteAsync(input);
    }

    [Authorize(EksabliPermissions.EmployeeAssignments.Edit)]
    [HttpPut("{id}")]
    public Task<EmployeeAssignmentDto> UpdateAsync(Guid id, UpdateEmployeeAssignmentDto input)
    {
        return _employeeAssignmentAppService.UpdateAsync(id, input);
    }

    [Authorize(EksabliPermissions.EmployeeAssignments.Delete)]
    [HttpDelete("{id}")]
    public Task RemoveAsync(Guid id)
    {
        return _employeeAssignmentAppService.RemoveAsync(id);
    }
}
