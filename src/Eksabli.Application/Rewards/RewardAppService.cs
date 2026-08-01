using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Rewards;

public class RewardAppService : ApplicationService, IRewardAppService
{
    private readonly IRewardRepository _repository;

    public RewardAppService(IRewardRepository repository)
    {
        _repository = repository;
    }

    public async Task<RewardDto> GetAsync(Guid id)
    {
        var reward = await _repository.GetAsync(id);
        return ObjectMapper.Map<Reward, RewardDto>(reward);
    }

    public async Task<PagedResultDto<RewardDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var (rewards, totalCount) = await _repository.GetListAsync(
            sorting: input.Sorting,
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount);

        return new PagedResultDto<RewardDto>(totalCount, ObjectMapper.Map<List<Reward>, List<RewardDto>>(rewards));
    }

    public async Task<RewardDto> CreateAsync(CreateUpdateRewardDto input)
    {
        var reward = Reward.Create(GuidGenerator.Create(), input.NameAr, input.NameEn, input.Type, input.PointsCost);
        ApplyInput(reward, input);
        await _repository.InsertAsync(reward);
        return ObjectMapper.Map<Reward, RewardDto>(reward);
    }

    public async Task<RewardDto> UpdateAsync(Guid id, CreateUpdateRewardDto input)
    {
        var reward = await _repository.GetAsync(id);
        reward.SetNames(input.NameAr, input.NameEn);
        reward.SetType(input.Type);
        reward.SetPointsCost(input.PointsCost);
        ApplyInput(reward, input);
        await _repository.UpdateAsync(reward);
        return ObjectMapper.Map<Reward, RewardDto>(reward);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private static void ApplyInput(Reward reward, CreateUpdateRewardDto input)
    {
        reward.SetStock(input.StockRemaining);
        reward.SetValidity(input.ValidFrom, input.ValidTo);
        reward.SetImageBlobName(input.ImageBlobName);
        reward.SetApprovalThresholdPoints(input.ApprovalThresholdPoints);
    }
}
