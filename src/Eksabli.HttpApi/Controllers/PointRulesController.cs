using System;
using System.Threading.Tasks;
using Eksabli.Permissions;
using Eksabli.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/point-rules")]
[Authorize(EksabliPermissions.PointRules.Default)]
public class PointRulesController : EksabliController
{
    private readonly IPointRuleAppService _pointRuleAppService;

    public PointRulesController(IPointRuleAppService pointRuleAppService)
    {
        _pointRuleAppService = pointRuleAppService;
    }

    [HttpGet("{id}")]
    public Task<PointRuleDto> GetAsync(Guid id)
    {
        return _pointRuleAppService.GetAsync(id);
    }

    [HttpGet]
    public Task<PagedResultDto<PointRuleDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _pointRuleAppService.GetListAsync(input);
    }

    [Authorize(EksabliPermissions.PointRules.Create)]
    [HttpPost]
    public Task<PointRuleDto> CreateAsync(CreateUpdatePointRuleDto input)
    {
        return _pointRuleAppService.CreateAsync(input);
    }

    [Authorize(EksabliPermissions.PointRules.Edit)]
    [HttpPut("{id}")]
    public Task<PointRuleDto> UpdateAsync(Guid id, CreateUpdatePointRuleDto input)
    {
        return _pointRuleAppService.UpdateAsync(id, input);
    }

    [Authorize(EksabliPermissions.PointRules.Delete)]
    [HttpDelete("{id}")]
    public Task DeleteAsync(Guid id)
    {
        return _pointRuleAppService.DeleteAsync(id);
    }
}
