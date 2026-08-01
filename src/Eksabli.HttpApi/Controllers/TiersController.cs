using System;
using System.Threading.Tasks;
using Eksabli.Permissions;
using Eksabli.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/tiers")]
[Authorize(EksabliPermissions.Tiers.Default)]
public class TiersController : EksabliController
{
    private readonly ITierAppService _tierAppService;

    public TiersController(ITierAppService tierAppService)
    {
        _tierAppService = tierAppService;
    }

    [HttpGet("{id}")]
    public Task<TierDto> GetAsync(Guid id)
    {
        return _tierAppService.GetAsync(id);
    }

    [HttpGet]
    public Task<PagedResultDto<TierDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _tierAppService.GetListAsync(input);
    }

    [Authorize(EksabliPermissions.Tiers.Create)]
    [HttpPost]
    public Task<TierDto> CreateAsync(CreateUpdateTierDto input)
    {
        return _tierAppService.CreateAsync(input);
    }

    [Authorize(EksabliPermissions.Tiers.Edit)]
    [HttpPut("{id}")]
    public Task<TierDto> UpdateAsync(Guid id, CreateUpdateTierDto input)
    {
        return _tierAppService.UpdateAsync(id, input);
    }

    [Authorize(EksabliPermissions.Tiers.Delete)]
    [HttpDelete("{id}")]
    public Task DeleteAsync(Guid id)
    {
        return _tierAppService.DeleteAsync(id);
    }
}
