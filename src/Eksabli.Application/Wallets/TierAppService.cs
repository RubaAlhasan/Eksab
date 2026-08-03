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
public class TierAppService : ApplicationService, ITierAppService
{
    private readonly ITierRepository _repository;

    public TierAppService(ITierRepository repository)
    {
        _repository = repository;
    }

    public async Task<TierDto> GetAsync(Guid id)
    {
        var tier = await _repository.GetAsync(id);
        return ObjectMapper.Map<Tier, TierDto>(tier);
    }

    public async Task<PagedResultDto<TierDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var (tiers, totalCount) = await _repository.GetListAsync(
            sorting: input.Sorting,
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount);

        return new PagedResultDto<TierDto>(totalCount, ObjectMapper.Map<List<Tier>, List<TierDto>>(tiers));
    }

    public async Task<TierDto> CreateAsync(CreateUpdateTierDto input)
    {
        var tier = Tier.Create(GuidGenerator.Create(), input.Name, input.MinLifetimePoints, input.Multiplier);
        await _repository.InsertAsync(tier);
        return ObjectMapper.Map<Tier, TierDto>(tier);
    }

    public async Task<TierDto> UpdateAsync(Guid id, CreateUpdateTierDto input)
    {
        var tier = await _repository.GetAsync(id);
        tier.SetName(input.Name);
        tier.SetMinLifetimePoints(input.MinLifetimePoints);
        tier.SetMultiplier(input.Multiplier);
        await _repository.UpdateAsync(tier);
        return ObjectMapper.Map<Tier, TierDto>(tier);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
