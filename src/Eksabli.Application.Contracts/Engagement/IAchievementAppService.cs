using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Engagement;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/AchievementsController.cs).
[RemoteService(IsEnabled = false)]
public interface IAchievementAppService : IApplicationService
{
    Task<AchievementDto> GetAsync(Guid id);

    // Platform-wide + this tenant's own achievements — see Achievement's own comment for why.
    Task<PagedResultDto<AchievementDto>> GetListAsync(PagedAndSortedResultRequestDto input);

    Task<AchievementDto> CreateAsync(CreateUpdateAchievementDto input);

    Task<AchievementDto> UpdateAsync(Guid id, CreateUpdateAchievementDto input);

    Task DeleteAsync(Guid id);

    Task<AchievementAwardDto> AwardAsync(AwardAchievementDto input);

    Task<List<AchievementAwardDto>> GetAwardsForMembershipAsync(Guid membershipId);
}
