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
[Route("api/app/achievement")]
[Authorize(EksabliPermissions.Achievements.Default)]
public class AchievementsController : EksabliController
{
    private readonly IAchievementAppService _achievementAppService;

    public AchievementsController(IAchievementAppService achievementAppService)
    {
        _achievementAppService = achievementAppService;
    }

    [HttpGet("{id}")]
    public Task<AchievementDto> GetAsync(Guid id)
    {
        return _achievementAppService.GetAsync(id);
    }

    [HttpGet]
    public Task<PagedResultDto<AchievementDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _achievementAppService.GetListAsync(input);
    }

    [Authorize(EksabliPermissions.Achievements.Create)]
    [HttpPost]
    public Task<AchievementDto> CreateAsync(CreateUpdateAchievementDto input)
    {
        return _achievementAppService.CreateAsync(input);
    }

    [Authorize(EksabliPermissions.Achievements.Edit)]
    [HttpPut("{id}")]
    public Task<AchievementDto> UpdateAsync(Guid id, CreateUpdateAchievementDto input)
    {
        return _achievementAppService.UpdateAsync(id, input);
    }

    [Authorize(EksabliPermissions.Achievements.Delete)]
    [HttpDelete("{id}")]
    public Task DeleteAsync(Guid id)
    {
        return _achievementAppService.DeleteAsync(id);
    }

    [Authorize(EksabliPermissions.Achievements.Award)]
    [HttpPost("award")]
    public Task<AchievementAwardDto> AwardAsync(AwardAchievementDto input)
    {
        return _achievementAppService.AwardAsync(input);
    }

    [HttpGet("membership/{membershipId}/awards")]
    public Task<List<AchievementAwardDto>> GetAwardsForMembershipAsync(Guid membershipId)
    {
        return _achievementAppService.GetAwardsForMembershipAsync(membershipId);
    }
}
