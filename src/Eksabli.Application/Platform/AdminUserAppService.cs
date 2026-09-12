using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Eksabli.CustomerProfiles;
using Eksabli.EmployeeAssignments;
using Eksabli.Memberships;
using Eksabli.Wallets;
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
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<Tier, Guid> _tierRepository;
    private readonly IRepository<PointsTransaction, Guid> _transactionRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    public AdminUserAppService(
        IRepository<CustomerProfile, Guid> customerProfileRepository,
        IEmployeeAssignmentRepository employeeAssignmentRepository,
        IIdentityUserRepository identityUserRepository,
        IRepository<Tenant, Guid> tenantRepository,
        IRepository<Membership, Guid> membershipRepository,
        IRepository<PointsWallet, Guid> walletRepository,
        IRepository<Tier, Guid> tierRepository,
        IRepository<PointsTransaction, Guid> transactionRepository,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter)
    {
        _customerProfileRepository = customerProfileRepository;
        _employeeAssignmentRepository = employeeAssignmentRepository;
        _identityUserRepository = identityUserRepository;
        _tenantRepository = tenantRepository;
        _membershipRepository = membershipRepository;
        _walletRepository = walletRepository;
        _tierRepository = tierRepository;
        _transactionRepository = transactionRepository;
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

    // Admin Portal > Users > Customer Details. Every business this customer has joined, each with its
    // own independent PointsWallet — the concrete UI expression of the two-realm identity design (one
    // global customer, N independent per-business balances). Cross-tenant, same
    // Disable<IMultiTenant>() shape GetStaffAsync already uses.
    public async Task<AdminCustomerDetailDto> GetCustomerDetailAsync(Guid customerId)
    {
        IdentityUser user;
        CustomerProfile? profile;
        using (_currentTenant.Change(null))
        {
            user = await _identityUserRepository.GetAsync(customerId); // throws -> 404 for a bad id
            profile = await _customerProfileRepository.FirstOrDefaultAsync(p => p.UserId == customerId);
        }

        List<AdminCustomerMembershipDto> memberships;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var customerMemberships = await _membershipRepository.GetListAsync(m => m.CustomerId == customerId);
            var membershipIds = customerMemberships.Select(m => m.Id).ToList();

            var wallets = await _walletRepository.GetListAsync(w => membershipIds.Contains(w.MembershipId));
            var walletByMembershipId = wallets.ToDictionary(w => w.MembershipId);

            var tierIds = wallets.Where(w => w.CurrentTierId.HasValue).Select(w => w.CurrentTierId!.Value).Distinct().ToList();
            var tierNameById = tierIds.Count == 0
                ? new Dictionary<Guid, string>()
                : (await _tierRepository.GetListAsync(t => tierIds.Contains(t.Id))).ToDictionary(t => t.Id, t => t.Name);

            var tenantIds = customerMemberships.Where(m => m.TenantId.HasValue).Select(m => m.TenantId!.Value).Distinct().ToList();
            var tenantNameById = tenantIds.Count == 0
                ? new Dictionary<Guid, string>()
                : (await _tenantRepository.GetListAsync(t => tenantIds.Contains(t.Id))).ToDictionary(t => t.Id, t => t.Name);

            memberships = customerMemberships
                .OrderByDescending(m => m.JoinedAt)
                .Select(m =>
                {
                    var wallet = walletByMembershipId.GetValueOrDefault(m.Id);
                    return new AdminCustomerMembershipDto
                    {
                        MembershipId = m.Id,
                        TenantId = m.TenantId ?? Guid.Empty,
                        BusinessName = m.TenantId.HasValue ? tenantNameById.GetValueOrDefault(m.TenantId.Value, string.Empty) : string.Empty,
                        Status = m.Status,
                        JoinedAt = m.JoinedAt,
                        Balance = wallet?.Balance ?? 0,
                        LifetimeEarned = wallet?.LifetimeEarned ?? 0,
                        LifetimeRedeemed = wallet?.LifetimeRedeemed ?? 0,
                        TierName = wallet?.CurrentTierId.HasValue == true ? tierNameById.GetValueOrDefault(wallet.CurrentTierId.Value) : null
                    };
                })
                .ToList();
        }

        return new AdminCustomerDetailDto
        {
            Id = customerId,
            FirstName = profile?.FirstName,
            LastName = profile?.LastName,
            Contact = user.PhoneNumber,
            IsActive = user.IsActive,
            CreationTime = profile?.CreationTime ?? user.CreationTime,
            Memberships = memberships
        };
    }

    // The ledger behind one row of GetCustomerDetailAsync's Memberships list. Switches ambient tenant
    // to the membership's own business (same technique as WalletAppService.GetMyTransactionHistoryAsync)
    // rather than Disable<IMultiTenant>() + manual filtering — this call is only ever about ONE business
    // at a time, so scoping the filter to exactly that tenant is both simpler and tighter than disabling
    // it outright. A membershipId that doesn't belong to tenantId resolves to "no wallet found" (the
    // filter simply won't match it), never another tenant's data.
    public async Task<PagedResultDto<PointsTransactionDto>> GetCustomerTransactionsAsync(Guid membershipId, Guid tenantId, PagedAndSortedResultRequestDto input)
    {
        using (_currentTenant.Change(tenantId))
        {
            var wallet = await _walletRepository.FirstOrDefaultAsync(w => w.MembershipId == membershipId)
                ?? throw new UserFriendlyException("No wallet found for this membership.");

            var queryable = await _transactionRepository.GetQueryableAsync();
            var query = queryable
                .Where(t => t.WalletId == wallet.Id)
                .OrderBy(input.Sorting.IsNullOrWhiteSpace() ? "CreationTime desc" : input.Sorting)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount);

            var transactions = await AsyncExecuter.ToListAsync(query);
            var totalCount = await AsyncExecuter.CountAsync(queryable.Where(t => t.WalletId == wallet.Id));

            var dtos = ObjectMapper.Map<List<PointsTransaction>, List<PointsTransactionDto>>(transactions);
            return new PagedResultDto<PointsTransactionDto>(totalCount, dtos);
        }
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
