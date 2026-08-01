using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Branches;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/BranchesController.cs).
[RemoteService(IsEnabled = false)]
public interface IBranchAppService : IApplicationService
{
    Task<BranchDto> GetAsync(Guid id);

    Task<PagedResultDto<BranchDto>> GetListAsync(PagedAndSortedResultRequestDto input);

    Task<BranchDto> CreateAsync(CreateUpdateBranchDto input);

    Task<BranchDto> UpdateAsync(Guid id, CreateUpdateBranchDto input);

    Task DeleteAsync(Guid id);
}
