using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Wallets;

[RemoteService(IsEnabled = false)]
public class PointRuleAppService : ApplicationService, IPointRuleAppService
{
    private readonly IPointRuleRepository _repository;

    public PointRuleAppService(IPointRuleRepository repository)
    {
        _repository = repository;
    }

    public async Task<PointRuleDto> GetAsync(Guid id)
    {
        var rule = await _repository.GetAsync(id);
        return ObjectMapper.Map<PointRule, PointRuleDto>(rule);
    }

    public async Task<PagedResultDto<PointRuleDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var (rules, totalCount) = await _repository.GetListAsync(
            sorting: input.Sorting,
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount);

        return new PagedResultDto<PointRuleDto>(totalCount, ObjectMapper.Map<List<PointRule>, List<PointRuleDto>>(rules));
    }

    public async Task<PointRuleDto> CreateAsync(CreateUpdatePointRuleDto input)
    {
        var existing = await _repository.FirstOrDefaultAsync(r => r.RuleType == input.RuleType);
        if (existing != null)
        {
            throw new UserFriendlyException($"A point rule of type '{input.RuleType}' already exists for this business.");
        }

        var rule = PointRule.Create(GuidGenerator.Create(), input.RuleType, input.PointsPerUnit);
        await _repository.InsertAsync(rule);
        return ObjectMapper.Map<PointRule, PointRuleDto>(rule);
    }

    public async Task<PointRuleDto> UpdateAsync(Guid id, CreateUpdatePointRuleDto input)
    {
        var rule = await _repository.GetAsync(id);
        rule.SetPointsPerUnit(input.PointsPerUnit);
        await _repository.UpdateAsync(rule);
        return ObjectMapper.Map<PointRule, PointRuleDto>(rule);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
