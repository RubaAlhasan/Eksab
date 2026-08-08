using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.BusinessProfiles;
using Eksabli.Memberships;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace Eksabli.Businesses;

[RemoteService(IsEnabled = false)]
public class AdminTenantAppService : ApplicationService, IAdminTenantAppService
{
    private readonly IRepository<BusinessProfile, Guid> _businessProfileRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<Tenant, Guid> _tenantGenericRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IDataFilter _dataFilter;

    public AdminTenantAppService(
        IRepository<BusinessProfile, Guid> businessProfileRepository,
        ITenantRepository tenantRepository,
        IRepository<Tenant, Guid> tenantGenericRepository,
        IRepository<Membership, Guid> membershipRepository,
        IDataFilter dataFilter)
    {
        _businessProfileRepository = businessProfileRepository;
        _tenantRepository = tenantRepository;
        _tenantGenericRepository = tenantGenericRepository;
        _membershipRepository = membershipRepository;
        _dataFilter = dataFilter;
    }

    public async Task<PagedResultDto<AdminTenantDto>> GetListAsync(AdminTenantFilterDto input)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var profiles = await _businessProfileRepository.GetListAsync(p =>
                !input.ApprovalStatus.HasValue || p.ApprovalStatus == input.ApprovalStatus.Value);

            var tenantIds = profiles.Select(p => p.TenantId!.Value).ToList();
            var tenantNameLookup = (await _tenantGenericRepository.GetListAsync(t => tenantIds.Contains(t.Id)))
                .ToDictionary(t => t.Id, t => t.Name);
            var memberCountLookup = await GetMemberCountLookupAsync(tenantIds);

            var dtos = profiles.Select(p => ToDto(
                p,
                tenantNameLookup.GetValueOrDefault(p.TenantId!.Value) ?? string.Empty,
                memberCountLookup.GetValueOrDefault(p.TenantId!.Value)));

            if (!input.FilterText.IsNullOrWhiteSpace())
            {
                dtos = dtos.Where(d => d.TenantName.Contains(input.FilterText!, StringComparison.OrdinalIgnoreCase));
            }

            var dtoList = dtos.OrderByDescending(d => d.CreationTime).ToList();
            var totalCount = dtoList.Count;
            var paged = dtoList.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

            return new PagedResultDto<AdminTenantDto>(totalCount, paged);
        }
    }

    public async Task<AdminTenantDto> GetAsync(Guid tenantId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var profile = await GetBusinessProfileAsync(tenantId);
            var tenant = await _tenantRepository.GetAsync(tenantId);
            var memberCountLookup = await GetMemberCountLookupAsync([tenantId]);
            return ToDto(profile, tenant.Name, memberCountLookup.GetValueOrDefault(tenantId));
        }
    }

    // Active-member count per tenant, counted at the database level (GroupBy translates to SQL
    // GROUP BY, not a client-side load of every Membership row) — same
    // IDataFilter.Disable<IMultiTenant>() scope as the rest of this service, called from within an
    // already-open `using` block by every caller above, not opening its own.
    private async Task<Dictionary<Guid, int>> GetMemberCountLookupAsync(List<Guid> tenantIds)
    {
        var membershipQueryable = await _membershipRepository.GetQueryableAsync();
        var counts = await AsyncExecuter.ToListAsync(
            membershipQueryable
                .Where(m => m.TenantId.HasValue && tenantIds.Contains(m.TenantId.Value) && m.Status == MembershipStatus.Active)
                .GroupBy(m => m.TenantId!.Value)
                .Select(g => new { TenantId = g.Key, Count = g.Count() }));

        return counts.ToDictionary(x => x.TenantId, x => x.Count);
    }

    public async Task<AdminTenantDto> ApproveAsync(Guid tenantId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var profile = await GetBusinessProfileAsync(tenantId);
            profile.Approve();
            await _businessProfileRepository.UpdateAsync(profile);

            var tenant = await _tenantRepository.GetAsync(tenantId);
            var memberCountLookup = await GetMemberCountLookupAsync([tenantId]);
            return ToDto(profile, tenant.Name, memberCountLookup.GetValueOrDefault(tenantId));
        }
    }

    public async Task<AdminTenantDto> SuspendAsync(Guid tenantId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var profile = await GetBusinessProfileAsync(tenantId);
            profile.Suspend();
            await _businessProfileRepository.UpdateAsync(profile);

            var tenant = await _tenantRepository.GetAsync(tenantId);
            var memberCountLookup = await GetMemberCountLookupAsync([tenantId]);
            return ToDto(profile, tenant.Name, memberCountLookup.GetValueOrDefault(tenantId));
        }
    }

    private async Task<BusinessProfile> GetBusinessProfileAsync(Guid tenantId)
    {
        return await _businessProfileRepository.FirstOrDefaultAsync(p => p.TenantId == tenantId)
            ?? throw new EntityNotFoundException(typeof(BusinessProfile), tenantId);
    }

    private static AdminTenantDto ToDto(BusinessProfile profile, string tenantName, int memberCount)
    {
        return new AdminTenantDto
        {
            TenantId = profile.TenantId!.Value,
            TenantName = tenantName,
            BusinessProfileId = profile.Id,
            CategoryId = profile.CategoryId,
            ApprovalStatus = profile.ApprovalStatus,
            CreationTime = profile.CreationTime,
            MemberCount = memberCount
        };
    }
}
