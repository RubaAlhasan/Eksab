using System;
using System.Threading.Tasks;
using Eksabli.Permissions;
using Eksabli.Rewards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/reward")]
[Authorize(EksabliPermissions.Rewards.Default)]
public class RewardsController : EksabliController
{
    private readonly IRewardAppService _rewardAppService;

    public RewardsController(IRewardAppService rewardAppService)
    {
        _rewardAppService = rewardAppService;
    }

    [HttpGet("{id}")]
    public Task<RewardDto> GetAsync(Guid id)
    {
        return _rewardAppService.GetAsync(id);
    }

    [HttpGet]
    public Task<PagedResultDto<RewardDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _rewardAppService.GetListAsync(input);
    }

    [Authorize(EksabliPermissions.Rewards.Create)]
    [HttpPost]
    public Task<RewardDto> CreateAsync(CreateUpdateRewardDto input)
    {
        return _rewardAppService.CreateAsync(input);
    }

    [Authorize(EksabliPermissions.Rewards.Edit)]
    [HttpPut("{id}")]
    public Task<RewardDto> UpdateAsync(Guid id, CreateUpdateRewardDto input)
    {
        return _rewardAppService.UpdateAsync(id, input);
    }

    [Authorize(EksabliPermissions.Rewards.Delete)]
    [HttpDelete("{id}")]
    public Task DeleteAsync(Guid id)
    {
        return _rewardAppService.DeleteAsync(id);
    }
}
