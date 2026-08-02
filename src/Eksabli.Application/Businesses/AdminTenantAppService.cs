using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.BusinessProfiles;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace Eksabli.Businesses;

public class AdminTenantAppService : ApplicationService, IAdminTenantAppService
{
    private readonly IRepository<BusinessProfile, Guid> _businessProfileRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<Tenant, Guid> _tenantGenericRepository;
    private readonly IDataFilter _dataFilter;

    public AdminTenantAppService(
        IRepository<BusinessProfile, Guid> businessProfileRepository,
        ITenantRepository tenantRepository,
        IRepository<Tenant, Guid> tenantGenericRepository,
        IDataFilter dataFilter)
    {
        _businessProfileRepository = businessProfileRepository;
        _tenantRepository = tenantRepository;
        _tenantGenericRepository = tenantGenericRepository;
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

            var dtos = profiles.Select(p => ToDto(p, tenantNameLookup.GetValueOrDefault(p.TenantId!.Value) ?? string.Empty));

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
            return ToDto(profile, tenant.Name);
        }
    }

    public async Task<AdminTenantDto> ApproveAsync(Guid tenantId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var profile = await GetBusinessProfileAsync(tenantId);
            profile.Approve();
            await _businessProfileRepository.UpdateAsync(profile);

            var tenant = await _tenantRepository.GetAsync(tenantId);
            return ToDto(profile, tenant.Name);
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
            return ToDto(profile, tenant.Name);
        }
    }

    private async Task<BusinessProfile> GetBusinessProfileAsync(Guid tenantId)
    {
        return await _businessProfileRepository.FirstOrDefaultAsync(p => p.TenantId == tenantId)
            ?? throw new EntityNotFoundException(typeof(BusinessProfile), tenantId);
    }

    private static AdminTenantDto ToDto(BusinessProfile profile, string tenantName)
    {
        return new AdminTenantDto
        {
            TenantId = profile.TenantId!.Value,
            TenantName = tenantName,
            BusinessProfileId = profile.Id,
            CategoryId = profile.CategoryId,
            ApprovalStatus = profile.ApprovalStatus,
            CreationTime = profile.CreationTime
        };
    }
}
