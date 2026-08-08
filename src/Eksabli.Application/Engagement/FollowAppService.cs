using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.CustomerProfiles;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace Eksabli.Engagement;

[RemoteService(IsEnabled = false)]
public class FollowAppService : ApplicationService, IFollowAppService
{
    private readonly IRepository<Follow, Guid> _repository;
    private readonly IRepository<CustomerProfile, Guid> _customerProfileRepository;
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    public FollowAppService(
        IRepository<Follow, Guid> repository,
        IRepository<CustomerProfile, Guid> customerProfileRepository,
        IIdentityUserRepository identityUserRepository,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter)
    {
        _repository = repository;
        _customerProfileRepository = customerProfileRepository;
        _identityUserRepository = identityUserRepository;
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

    // Enriched with name/phone (same cross-realm join shape as Memberships.MemberDto) — GetMyFollowsAsync
    // above stays bare (FollowDto), a customer doesn't need their own name echoed back to them.
    public async Task<PagedResultDto<FollowerDto>> GetFollowersAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _repository.CountAsync();
        var follows = await _repository.GetPagedListAsync(
            input.SkipCount, input.MaxResultCount, input.Sorting ?? "FollowedAt desc");

        var customerIds = follows.Select(f => f.CustomerId).ToList();

        List<IdentityUser> users;
        List<CustomerProfile> profiles;
        using (_currentTenant.Change(null))
        {
            users = await _identityUserRepository.GetListByIdsAsync(customerIds);
            profiles = await _customerProfileRepository.GetListAsync(p => customerIds.Contains(p.UserId));
        }
        var userById = users.ToDictionary(u => u.Id);
        var profileByUserId = profiles.ToDictionary(p => p.UserId);

        var dtos = follows.Select(f =>
        {
            var profile = profileByUserId.GetValueOrDefault(f.CustomerId);
            var user = userById.GetValueOrDefault(f.CustomerId);
            return new FollowerDto
            {
                Id = f.Id,
                CustomerId = f.CustomerId,
                FirstName = profile?.FirstName,
                LastName = profile?.LastName,
                PhoneNumber = user?.PhoneNumber,
                FollowedAt = f.FollowedAt
            };
        }).ToList();

        return new PagedResultDto<FollowerDto>(totalCount, dtos);
    }
}
