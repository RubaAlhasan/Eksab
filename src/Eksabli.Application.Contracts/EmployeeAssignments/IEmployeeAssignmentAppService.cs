using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.EmployeeAssignments;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/EmployeeAssignmentsController.cs).
[RemoteService(IsEnabled = false)]
public interface IEmployeeAssignmentAppService : IApplicationService
{
    Task<PagedResultDto<EmployeeAssignmentDto>> GetListAsync(PagedAndSortedResultRequestDto input);

    Task<InviteEmployeeResultDto> InviteAsync(InviteEmployeeDto input);

    Task<EmployeeAssignmentDto> UpdateAsync(Guid id, UpdateEmployeeAssignmentDto input);

    Task RemoveAsync(Guid id);
}
