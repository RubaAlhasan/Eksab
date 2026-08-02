using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Billing;

public class SubscriptionPlanAppService : ApplicationService, ISubscriptionPlanAppService
{
    private readonly ISubscriptionPlanRepository _repository;

    public SubscriptionPlanAppService(ISubscriptionPlanRepository repository)
    {
        _repository = repository;
    }

    public async Task<SubscriptionPlanDto> GetAsync(Guid id)
    {
        var plan = await _repository.GetAsync(id);
        return ObjectMapper.Map<SubscriptionPlan, SubscriptionPlanDto>(plan);
    }

    public async Task<PagedResultDto<SubscriptionPlanDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var (plans, totalCount) = await _repository.GetListAsync(
            sorting: input.Sorting,
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount);

        return new PagedResultDto<SubscriptionPlanDto>(totalCount, ObjectMapper.Map<List<SubscriptionPlan>, List<SubscriptionPlanDto>>(plans));
    }

    public async Task<SubscriptionPlanDto> CreateAsync(CreateUpdateSubscriptionPlanDto input)
    {
        var plan = SubscriptionPlan.Create(GuidGenerator.Create(), input.Name, input.MonthlyPrice, input.FeatureLimitsJson, input.IsTrialDefault);
        await _repository.InsertAsync(plan);
        return ObjectMapper.Map<SubscriptionPlan, SubscriptionPlanDto>(plan);
    }

    public async Task<SubscriptionPlanDto> UpdateAsync(Guid id, CreateUpdateSubscriptionPlanDto input)
    {
        var plan = await _repository.GetAsync(id);
        plan.SetName(input.Name);
        plan.SetMonthlyPrice(input.MonthlyPrice);
        plan.SetFeatureLimitsJson(input.FeatureLimitsJson);
        plan.SetIsTrialDefault(input.IsTrialDefault);
        await _repository.UpdateAsync(plan);
        return ObjectMapper.Map<SubscriptionPlan, SubscriptionPlanDto>(plan);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
