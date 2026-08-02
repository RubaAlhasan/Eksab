using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Engagement;
using Eksabli.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/follow")]
[Authorize]
public class FollowsController : EksabliController
{
    private readonly IFollowAppService _followAppService;

    public FollowsController(IFollowAppService followAppService)
    {
        _followAppService = followAppService;
    }

    [HttpPost("{tenantId}")]
    public Task FollowAsync(Guid tenantId)
    {
        return _followAppService.FollowAsync(tenantId);
    }

    [HttpDelete("{tenantId}")]
    public Task UnfollowAsync(Guid tenantId)
    {
        return _followAppService.UnfollowAsync(tenantId);
    }

    [HttpGet("my")]
    public Task<List<FollowDto>> GetMyFollowsAsync()
    {
        return _followAppService.GetMyFollowsAsync();
    }

    [Authorize(EksabliPermissions.Followers.View)]
    [HttpGet("followers")]
    public Task<PagedResultDto<FollowDto>> GetFollowersAsync([FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _followAppService.GetFollowersAsync(input);
    }
}
