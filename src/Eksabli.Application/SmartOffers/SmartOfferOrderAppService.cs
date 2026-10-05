using System;
using System.Threading.Tasks;
using Eksabli.CustomerProfiles;
using Eksabli.EmployeeAssignments;
using Eksabli.Memberships;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace Eksabli.SmartOffers;

// Staff counter for Buy Now orders. Role-gated like POS redemptions: an order is money changing hands at the till, so
// the state change is checked here, not left to the UI.
[RemoteService(IsEnabled = false)]
public class SmartOfferOrderAppService : SmartOfferServiceBase, ISmartOfferOrderAppService
{
    private readonly IRepository<EmployeeAssignment, Guid> _employeeAssignmentRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<CustomerProfile, Guid> _customerProfileRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IRepository<SmartOffer, Guid> _offerRepository;

    private const int MaxHistoryPageSize = 50;

    public SmartOfferOrderAppService(
        IRepository<SmartOfferOrder, Guid> orderRepository,
        IRepository<SmartOfferInventory, Guid> inventoryRepository,
        IRepository<EmployeeAssignment, Guid> employeeAssignmentRepository,
        IRepository<Membership, Guid> membershipRepository,
        IRepository<CustomerProfile, Guid> customerProfileRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IRepository<SmartOffer, Guid> offerRepository)
        : base(orderRepository, inventoryRepository)
    {
        _employeeAssignmentRepository = employeeAssignmentRepository;
        _membershipRepository = membershipRepository;
        _customerProfileRepository = customerProfileRepository;
        _identityUserRepository = identityUserRepository;
        _offerRepository = offerRepository;
    }

    // Lookup is read-only and open to any counter role, so a cashier can check a code before deciding anything.
    public async Task<SmartOfferOrderStaffDto> LookupAsync(SmartOfferOrderCodeDto input)
    {
        await CheckStaffRoleAsync(_employeeAssignmentRepository, EmployeeRole.Owner, EmployeeRole.BranchManager, EmployeeRole.Cashier);

        var order = await FindByCodeAsync(input.Code);
        return await ToStaffDtoAsync(order);
    }

    public async Task<SmartOfferOrderStaffDto> CompleteAsync(SmartOfferOrderCodeDto input)
    {
        var (employeeId, branchId) = await CheckStaffRoleAsync(
            _employeeAssignmentRepository,
            EmployeeRole.Owner,
            EmployeeRole.BranchManager,
            EmployeeRole.Cashier);

        var normalized = NormalizeCode(input.Code);

        return await ExecuteWithRetryAsync(async () =>
        {
            var nowUtc = NowUtc;
            var order = await OrderRepository.FirstOrDefaultAsync(o => o.Code == normalized)
                ?? throw new UserFriendlyException("Invalid or already-used code.");

            // Complete refuses a lapsed order itself, so a stale Pending row can never be honored at the till.
            order.Complete(nowUtc, employeeId, branchId);
            await OrderRepository.UpdateAsync(order, autoSave: true);

            // The held units become sold, in the same unit of work so the counts can never disagree with the order.
            var inventory = await InventoryRepository.FirstOrDefaultAsync(i => i.SlotId == order.SlotId && i.ServiceDate == order.ServiceDate);
            if (inventory != null)
            {
                inventory.Confirm(order.Quantity);
                await InventoryRepository.UpdateAsync(inventory, autoSave: true);
            }

            return await ToStaffDtoAsync(order);
        });
    }

    public async Task<SmartOfferOrderStaffDto> RejectAsync(RejectSmartOfferOrderDto input)
    {
        var (employeeId, _) = await CheckStaffRoleAsync(
            _employeeAssignmentRepository,
            EmployeeRole.Owner,
            EmployeeRole.BranchManager,
            EmployeeRole.Cashier);

        var normalized = NormalizeCode(input.Code);

        return await ExecuteWithRetryAsync(async () =>
        {
            var order = await OrderRepository.FirstOrDefaultAsync(o => o.Code == normalized)
                ?? throw new UserFriendlyException("Invalid or already-used code.");

            order.Reject(employeeId, input.Reason);
            await OrderRepository.UpdateAsync(order, autoSave: true);

            // Refusing never costs the customer anything. The units go back on sale.
            var inventory = await InventoryRepository.FirstOrDefaultAsync(i => i.SlotId == order.SlotId && i.ServiceDate == order.ServiceDate);
            if (inventory != null)
            {
                inventory.Release(order.Quantity);
                await InventoryRepository.UpdateAsync(inventory, autoSave: true);
            }

            return await ToStaffDtoAsync(order);
        });
    }

    public async Task<PagedResultDto<SmartOfferOrderStaffDto>> GetHistoryAsync(PagedAndSortedResultRequestDto input)
    {
        await CheckStaffRoleAsync(_employeeAssignmentRepository, EmployeeRole.Owner, EmployeeRole.BranchManager, EmployeeRole.Cashier);

        // Only settled orders: a Pending one is still on the counter, so it is shown by lookup, not as a sale.
        var settled = (await OrderRepository.GetQueryableAsync())
            .Where(o => o.Status != SmartOfferOrderStatus.Pending);

        var total = await AsyncExecuter.CountAsync(settled);
        var orders = await AsyncExecuter.ToListAsync(
            settled
                .OrderByDescending(o => o.CreationTime)
                .Skip(input.SkipCount)
                .Take(Math.Min(input.MaxResultCount, MaxHistoryPageSize)));

        // Resolve the customer for every row in one pass per table, not one query per row.
        var membershipIds = orders.Select(o => o.MembershipId).Distinct().ToList();
        var memberships = await _membershipRepository.GetListAsync(m => membershipIds.Contains(m.Id));
        var customerIdByMembership = memberships.ToDictionary(m => m.Id, m => m.CustomerId);
        var customerIds = customerIdByMembership.Values.Distinct().ToList();

        List<CustomerProfile> profiles;
        using (CurrentTenant.Change(null)) // CustomerProfile is Host-realm, as in PosAppService.
        {
            profiles = await _customerProfileRepository.GetListAsync(p => customerIds.Contains(p.UserId));
        }

        var users = await _identityUserRepository.GetListAsync(u => customerIds.Contains(u.Id));

        var offerIds = orders.Select(o => o.SmartOfferId).Distinct().ToList();
        var offersById = offerIds.Count == 0
            ? new Dictionary<Guid, SmartOffer>()
            : (await _offerRepository.GetListAsync(o => offerIds.Contains(o.Id))).ToDictionary(o => o.Id);
        var nowUtc = NowUtc;

        var dtos = orders.Select(order =>
        {
            var dto = ObjectMapper.Map<SmartOfferOrder, SmartOfferOrderStaffDto>(order);
            dto.Status = order.HasLapsed(nowUtc) ? SmartOfferOrderStatus.Expired : order.Status;
            dto.ServerNowUtc = nowUtc;

            if (offersById.TryGetValue(order.SmartOfferId, out var offer))
            {
                dto.OfferDescriptionAr = offer.DescriptionAr;
                dto.OfferDescriptionEn = offer.DescriptionEn;
            }

            if (customerIdByMembership.TryGetValue(order.MembershipId, out var customerId))
            {
                var profile = profiles.FirstOrDefault(p => p.UserId == customerId);
                var name = profile == null ? null : $"{profile.FirstName} {profile.LastName}".Trim();
                dto.CustomerName = string.IsNullOrWhiteSpace(name) ? null : name;
                dto.CustomerPhone = users.FirstOrDefault(u => u.Id == customerId)?.PhoneNumber;
            }

            return dto;
        }).ToList();

        return new PagedResultDto<SmartOfferOrderStaffDto>(total, dtos);
    }

    // Staff type codes as the customer app shows them, so spaces and hyphens are stripped and case is ignored.
    private static string NormalizeCode(string code) =>
        (code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty).ToUpperInvariant();

    private async Task<SmartOfferOrder> FindByCodeAsync(string code)
    {
        var normalized = NormalizeCode(code);
        return await OrderRepository.FirstOrDefaultAsync(o => o.Code == normalized)
            ?? throw new UserFriendlyException("Invalid or already-used code.");
    }

    private async Task<SmartOfferOrderStaffDto> ToStaffDtoAsync(SmartOfferOrder order)
    {
        var nowUtc = NowUtc;
        var membership = await _membershipRepository.GetAsync(order.MembershipId);
        var (name, phone) = await ResolveCustomerIdentityAsync(_customerProfileRepository, _identityUserRepository, membership.CustomerId);

        var dto = ObjectMapper.Map<SmartOfferOrder, SmartOfferOrderStaffDto>(order);
        dto.Status = order.HasLapsed(nowUtc) ? SmartOfferOrderStatus.Expired : order.Status;
        dto.ServerNowUtc = nowUtc;
        dto.CustomerName = name;
        dto.CustomerPhone = phone;

        var offer = await _offerRepository.FindAsync(order.SmartOfferId);
        if (offer != null)
        {
            dto.OfferDescriptionAr = offer.DescriptionAr;
            dto.OfferDescriptionEn = offer.DescriptionEn;
        }

        return dto;
    }
}
