using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Rewards;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/RewardsController.cs).
[RemoteService(IsEnabled = false)]
public interface IRewardAppService : IApplicationService
{
    Task<RewardDto> GetAsync(Guid id);

    Task<PagedResultDto<RewardDto>> GetListAsync(PagedAndSortedResultRequestDto input);

    Task<RewardDto> CreateAsync(CreateUpdateRewardDto input);

    Task<RewardDto> UpdateAsync(Guid id, CreateUpdateRewardDto input);

    Task DeleteAsync(Guid id);
}
