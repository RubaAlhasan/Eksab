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
        if (input.RuleType == PointRuleType.PerCurrencyUnit && input.Currency == null)
        {
            throw new UserFriendlyException("A currency is required for a per-currency-unit point rule.");
        }

        if (input.RuleType == PointRuleType.PerVisit && input.Currency != null)
        {
            throw new UserFriendlyException("A per-visit point rule has no currency.");
        }

        var existing = await _repository.FirstOrDefaultAsync(r => r.RuleType == input.RuleType && r.Currency == input.Currency);
        if (existing != null)
        {
            var message = input.RuleType == PointRuleType.PerCurrencyUnit
                ? $"A point rule of type '{input.RuleType}' for currency '{input.Currency}' already exists for this business."
                : $"A point rule of type '{input.RuleType}' already exists for this business.";
            throw new UserFriendlyException(message);
        }

        var rule = PointRule.Create(GuidGenerator.Create(), input.RuleType, input.PointsPerUnit, input.Currency);
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
