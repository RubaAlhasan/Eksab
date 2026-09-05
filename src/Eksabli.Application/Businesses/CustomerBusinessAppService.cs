using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.Branches;
using Eksabli.BusinessProfiles;
using Eksabli.Platform;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace Eksabli.Businesses;

// Customer-facing business directory — the read side the consumer app needs but the
// Business/Admin services deliberately don't provide. Everything here is scoped to
// Approved tenants only; a Pending or Suspended business must not be discoverable,
// which mirrors the guard MembershipAppService.JoinAsync already applies at join time.
//
// [Authorize] with no permission: any signed-in customer may browse. Customers hold no
// tenant-scoped permissions, so requiring one would lock them all out.
[Authorize]
[RemoteService(IsEnabled = false)]
public class CustomerBusinessAppService : ApplicationService, ICustomerBusinessAppService
{
    private readonly IRepository<BusinessProfile, Guid> _businessProfileRepository;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly IRepository<Category, Guid> _categoryRepository;
    private readonly IRepository<Branch, Guid> _branchRepository;
    private readonly IDataFilter _dataFilter;

    public CustomerBusinessAppService(
        IRepository<BusinessProfile, Guid> businessProfileRepository,
        IRepository<Tenant, Guid> tenantRepository,
        IRepository<Category, Guid> categoryRepository,
        IRepository<Branch, Guid> branchRepository,
        IDataFilter dataFilter)
    {
        _businessProfileRepository = businessProfileRepository;
        _tenantRepository = tenantRepository;
        _categoryRepository = categoryRepository;
        _branchRepository = branchRepository;
        _dataFilter = dataFilter;
    }

    public async Task<PagedResultDto<CustomerBusinessDto>> GetListAsync(CustomerBusinessFilterDto input)
    {
        var all = await BuildAsync(
            profileFilter: p => input.CategoryId == null || p.CategoryId == input.CategoryId,
            latitude: input.Latitude,
            longitude: input.Longitude);

        if (!input.FilterText.IsNullOrWhiteSpace())
        {
            var text = input.FilterText!.Trim();
            all = all
                .Where(d =>
                    d.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                    (d.CategoryNameEn ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase) ||
                    (d.CategoryNameAr ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Nearest-first only makes sense when the caller told us where they are.
        var ordered = (input.Latitude.HasValue && input.Longitude.HasValue
                ? all.OrderBy(d => d.DistanceKm ?? double.MaxValue).ThenBy(d => d.Name)
                : all.OrderBy(d => d.Name))
            .ToList();

        return new PagedResultDto<CustomerBusinessDto>(
            ordered.Count,
            ordered.Skip(input.SkipCount).Take(input.MaxResultCount).ToList());
    }

    public async Task<CustomerBusinessDto> GetAsync(Guid tenantId)
    {
        var results = await BuildAsync(p => p.TenantId == tenantId);

        // Not-found rather than forbidden for a Pending/Suspended tenant: whether a
        // given business exists but is unapproved isn't a customer's concern.
        return results.SingleOrDefault()
            ?? throw new EntityNotFoundException(typeof(BusinessProfile), tenantId);
    }

    public async Task<List<CustomerBusinessDto>> GetManyAsync(CustomerBusinessLookupDto input)
    {
        if (input.TenantIds.Count == 0)
        {
            return new List<CustomerBusinessDto>();
        }

        // Distinct guards against a caller passing the same tenant once per wallet row.
        var ids = input.TenantIds.Distinct().ToList();
        return await BuildAsync(p => p.TenantId != null && ids.Contains(p.TenantId.Value));
    }

    // Single place that assembles the projection: profiles (Approved only) joined to
    // tenant names, category names and branch data.
    private async Task<List<CustomerBusinessDto>> BuildAsync(
        System.Linq.Expressions.Expression<Func<BusinessProfile, bool>> profileFilter,
        double? latitude = null,
        double? longitude = null)
    {
        // BusinessProfile, Branch and Tenant are all multi-tenant-filtered; a customer
        // is in the Host realm, so without this every query would come back empty.
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var profiles = (await _businessProfileRepository.GetListAsync(profileFilter))
                .Where(p => p.ApprovalStatus == TenantApprovalStatus.Approved && p.TenantId.HasValue)
                .ToList();

            if (profiles.Count == 0)
            {
                return new List<CustomerBusinessDto>();
            }

            var tenantIds = profiles.Select(p => p.TenantId!.Value).ToList();

            var tenantNames = (await _tenantRepository.GetListAsync(t => tenantIds.Contains(t.Id)))
                .ToDictionary(t => t.Id, t => t.Name);

            var categoryIds = profiles.Where(p => p.CategoryId.HasValue)
                .Select(p => p.CategoryId!.Value).Distinct().ToList();
            var categories = categoryIds.Count == 0
                ? new Dictionary<Guid, Category>()
                : (await _categoryRepository.GetListAsync(c => categoryIds.Contains(c.Id)))
                    .ToDictionary(c => c.Id);

            var branches = (await _branchRepository.GetListAsync(b => b.TenantId != null && tenantIds.Contains(b.TenantId.Value)))
                .GroupBy(b => b.TenantId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            return profiles.Select(p =>
            {
                var tenantId = p.TenantId!.Value;
                var tenantBranches = branches.GetValueOrDefault(tenantId) ?? new List<Branch>();
                var category = p.CategoryId.HasValue
                    ? categories.GetValueOrDefault(p.CategoryId.Value)
                    : null;

                return new CustomerBusinessDto
                {
                    TenantId = tenantId,
                    // A tenant row should always exist for a profile, but don't 500 the
                    // whole directory over one orphaned profile.
                    Name = tenantNames.GetValueOrDefault(tenantId) ?? string.Empty,
                    CategoryId = p.CategoryId,
                    CategoryNameAr = category?.NameAr,
                    CategoryNameEn = category?.NameEn,
                    DescriptionAr = p.DescriptionAr,
                    DescriptionEn = p.DescriptionEn,
                    Website = p.Website,
                    BusinessProfileId = p.Id,
                    HasLogo = !p.LogoBlobName.IsNullOrWhiteSpace(),
                    BranchCount = tenantBranches.Count,
                    DistanceKm = NearestBranchDistanceKm(tenantBranches, latitude, longitude),
                };
            }).ToList();
        }
    }

    private static double? NearestBranchDistanceKm(
        IReadOnlyCollection<Branch> branches,
        double? latitude,
        double? longitude)
    {
        if (!latitude.HasValue || !longitude.HasValue)
        {
            return null;
        }

        var located = branches
            .Where(b => b.Latitude.HasValue && b.Longitude.HasValue)
            .Select(b => HaversineKm(latitude.Value, longitude.Value, b.Latitude!.Value, b.Longitude!.Value))
            .ToList();

        return located.Count == 0 ? null : located.Min();
    }

    // Straight-line distance. Good enough for a "2.1 km away" label; swap for a real
    // PostGIS distance query if this ever needs to drive ranking at scale.
    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
