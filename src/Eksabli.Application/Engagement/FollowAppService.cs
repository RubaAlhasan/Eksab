using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace Eksabli.Engagement;

[RemoteService(IsEnabled = false)]
public class FollowAppService : ApplicationService, IFollowAppService
{
    private readonly IRepository<Follow, Guid> _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    public FollowAppService(IRepository<Follow, Guid> repository, ICurrentTenant currentTenant, IDataFilter dataFilter)
    {
        _repository = repository;
        _currentTenant = currentTenant;
        _dataFilter = dataFilter;
    }

    public async Task FollowAsync(Guid tenantId)
    {
        var customerId = CurrentUser.GetId();

        using (_currentTenant.Change(tenantId))
        {
            var existing = await _repository.FirstOrDefaultAsync(f => f.CustomerId == customerId);
            if (existing != null)
            {
                return; // idempotent
            }

            var follow = Follow.Create(GuidGenerator.Create(), customerId, Clock.Now);
            await _repository.InsertAsync(follow);
        }
    }

    public async Task UnfollowAsync(Guid tenantId)
    {
        var customerId = CurrentUser.GetId();

        using (_currentTenant.Change(tenantId))
        {
            var existing = await _repository.FirstOrDefaultAsync(f => f.CustomerId == customerId);
            if (existing != null)
            {
                await _repository.DeleteAsync(existing);
            }
        }
    }

    public async Task<List<FollowDto>> GetMyFollowsAsync()
    {
        var customerId = CurrentUser.GetId();

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var follows = await _repository.GetListAsync(f => f.CustomerId == customerId);
            return ObjectMapper.Map<List<Follow>, List<FollowDto>>(follows);
        }
    }

    public async Task<PagedResultDto<FollowDto>> GetFollowersAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _repository.CountAsync();
        var follows = await _repository.GetPagedListAsync(
            input.SkipCount, input.MaxResultCount, input.Sorting ?? "FollowedAt desc");

        return new PagedResultDto<FollowDto>(totalCount, ObjectMapper.Map<List<Follow>, List<FollowDto>>(follows));
    }
}
