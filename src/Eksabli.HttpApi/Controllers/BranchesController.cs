using System;
using System.Threading.Tasks;
using Eksabli.Branches;
using Eksabli.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/branches")]
[Authorize(EksabliPermissions.Branches.Default)]
public class BranchesController : EksabliController
{
    private readonly IBranchAppService _branchAppService;

    public BranchesController(IBranchAppService branchAppService)
    {
        _branchAppService = branchAppService;
    }

    [HttpGet("{id}")]
    public Task<BranchDto> GetAsync(Guid id)
    {
        return _branchAppService.GetAsync(id);
    }

    [HttpGet]
    public Task<PagedResultDto<BranchDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _branchAppService.GetListAsync(input);
    }

    [Authorize(EksabliPermissions.Branches.Create)]
    [HttpPost]
    public Task<BranchDto> CreateAsync(CreateUpdateBranchDto input)
    {
        return _branchAppService.CreateAsync(input);
    }

    [Authorize(EksabliPermissions.Branches.Edit)]
    [HttpPut("{id}")]
    public Task<BranchDto> UpdateAsync(Guid id, CreateUpdateBranchDto input)
    {
        return _branchAppService.UpdateAsync(id, input);
    }

    [Authorize(EksabliPermissions.Branches.Delete)]
    [HttpDelete("{id}")]
    public Task DeleteAsync(Guid id)
    {
        return _branchAppService.DeleteAsync(id);
    }
}
