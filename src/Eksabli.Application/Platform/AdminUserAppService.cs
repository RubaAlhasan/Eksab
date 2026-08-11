using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.CustomerProfiles;
using Eksabli.EmployeeAssignments;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace Eksabli.Platform;

[RemoteService(IsEnabled = false)]
public class AdminUserAppService : ApplicationService, IAdminUserAppService
{
    private readonly IRepository<CustomerProfile, Guid> _customerProfileRepository;
    private readonly IEmployeeAssignmentRepository _employeeAssignmentRepository;
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    public AdminUserAppService(
        IRepository<CustomerProfile, Guid> customerProfileRepository,
        IEmployeeAssignmentRepository employeeAssignmentRepository,
        IIdentityUserRepository identityUserRepository,
        IRepository<Tenant, Guid> tenantRepository,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter)
    {
        _customerProfileRepository = customerProfileRepository;
        _employeeAssignmentRepository = employeeAssignmentRepository;
        _identityUserRepository = identityUserRepository;
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
        _dataFilter = dataFilter;
    }

    // Cross-tenant/cross-realm "user directory" for platform staff — see prototype/admin/users.html.
    // Combines two genuinely different data sources in-memory, same "acceptable at this scale" approach
    // AdminTenantAppService/MembershipAppService already use for their own cross-cutting admin lists:
    //  - Customers: Host-realm CustomerProfile + IdentityUser (phone as Contact, no BusinessName — a
    //    customer isn't tied to one business).
    //  - Staff: EmployeeAssignment (cross-tenant, via Disable<IMultiTenant>()) + each assignment's own
    //    tenant-scoped IdentityUser (email as Contact) + that tenant's real Tenant.Name as BusinessName
    //    — NOT a hardcoded business name (the prototype's own demo data hardcodes "Cedar & Bean Coffee"
    //    for every employee; every real row here resolves its own actual tenant).
    // Platform-staff accounts (seeded Host admin, Support Agent, ...) are deliberately excluded — see
    // AdminUserType's own comment for why; they belong on the stock Identity > Users page instead.
    public async Task<PagedResultDto<AdminUserDto>> GetListAsync(AdminUserFilterDto input)
    {
        var results = new List<AdminUserDto>();

        if (!input.Type.HasValue || input.Type.Value == AdminUserType.Customer)
        {
            results.AddRange(await GetCustomersAsync());
        }

        if (!input.Type.HasValue || input.Type.Value == AdminUserType.Staff)
        {
            results.AddRange(await GetStaffAsync());
        }

        IEnumerable<AdminUserDto> filtered = results;
        if (!input.FilterText.IsNullOrWhiteSpace())
        {
            var filterText = input.FilterText!;
            filtered = filtered.Where(d =>
                (d.FirstName?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (d.LastName?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (d.Contact?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (d.BusinessName?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var list = filtered.OrderByDescending(d => d.CreationTime).ToList();
        var totalCount = list.Count;
        var paged = list.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<AdminUserDto>(totalCount, paged);
    }

    private async Task<List<AdminUserDto>> GetCustomersAsync()
    {
        var profiles = await _customerProfileRepository.GetListAsync();
        var userIds = profiles.Select(p => p.UserId).ToList();

        List<IdentityUser> users;
        using (_currentTenant.Change(null))
        {
            // IIdentityUserRepository is a curated interface (no generic predicate/queryable access) —
            // GetListByIdsAsync is its own purpose-built batch lookup, same as MembershipAppService uses.
            users = await _identityUserRepository.GetListByIdsAsync(userIds);
        }
        var userById = users.ToDictionary(u => u.Id);

        return profiles.Select(p =>
        {
            var user = userById.GetValueOrDefault(p.UserId);
            return new AdminUserDto
            {
                Id = p.UserId,
                Type = AdminUserType.Customer,
                FirstName = p.FirstName,
                LastName = p.LastName,
                BusinessName = null,
                Contact = user?.PhoneNumber,
                IsActive = user?.IsActive ?? true,
                CreationTime = p.CreationTime,
            };
        }).ToList();
    }

    private async Task<List<AdminUserDto>> GetStaffAsync()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var (assignments, _) = await _employeeAssignmentRepository.GetListAsync(maxResultCount: int.MaxValue);

            var userIds = assignments.Select(a => a.UserId).Distinct().ToList();
            var users = await _identityUserRepository.GetListByIdsAsync(userIds);
            var userById = users.ToDictionary(u => u.Id);

            var tenantIds = assignments.Where(a => a.TenantId.HasValue).Select(a => a.TenantId!.Value).Distinct().ToList();
            var tenantNameById = (await _tenantRepository.GetListAsync(t => tenantIds.Contains(t.Id)))
                .ToDictionary(t => t.Id, t => t.Name);

            return assignments.Select(a =>
            {
                var user = userById.GetValueOrDefault(a.UserId);
                return new AdminUserDto
                {
                    Id = a.UserId,
                    Type = AdminUserType.Staff,
                    FirstName = null,
                    LastName = null,
                    BusinessName = a.TenantId.HasValue ? tenantNameById.GetValueOrDefault(a.TenantId.Value) : null,
                    Contact = user?.Email,
                    IsActive = user?.IsActive ?? true,
                    CreationTime = a.CreationTime,
                };
            }).ToList();
        }
    }
}
