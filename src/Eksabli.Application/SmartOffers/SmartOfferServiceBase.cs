using System;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.CustomerProfiles;
using Eksabli.EmployeeAssignments;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace Eksabli.SmartOffers;

// Shared plumbing for the three SmartOffer application services: the server clock, stock lookups, the order-code
// generator, the staff role gate, and the retry loop that makes contended stock writes safe.
public abstract class SmartOfferServiceBase : ApplicationService
{
    // Attempts before a stock conflict is reported to the customer. Each conflict means another buyer changed the
    // same slot first, so a retry sees fresh counts and is usually the answer.
    protected const int MaxStockAttempts = 5;

    protected const int MaxOrderCodeAttempts = 5;

    protected readonly IRepository<SmartOfferOrder, Guid> OrderRepository;
    protected readonly IRepository<SmartOfferInventory, Guid> InventoryRepository;

    protected SmartOfferServiceBase(
        IRepository<SmartOfferOrder, Guid> orderRepository,
        IRepository<SmartOfferInventory, Guid> inventoryRepository)
    {
        OrderRepository = orderRepository;
        InventoryRepository = inventoryRepository;
    }

    // Every price decision is made against UTC, so this has to be a true UTC instant regardless of the host's clock
    // kind. ABP's IClock reports local time for some configurations.
    protected DateTime NowUtc => Clock.Kind == DateTimeKind.Utc ? Clock.Now : Clock.Now.ToUniversalTime();

    protected static int? RemainingFor(SmartOfferInventory? inventory, int? capacity) =>
        inventory != null ? inventory.Remaining(capacity) : capacity;

    // Creates the day's stock row for a slot if no one has yet. Done in its own unit of work so a losing race (two
    // buyers creating the same row) can be caught without poisoning the buyer's transaction.
    protected async Task EnsureInventoryAsync(Guid smartOfferId, Guid slotId, DateOnly serviceDate)
    {
        if (await InventoryRepository.AnyAsync(i => i.SlotId == slotId && i.ServiceDate == serviceDate))
        {
            return;
        }

        try
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await InventoryRepository.InsertAsync(
                SmartOfferInventory.Create(GuidGenerator.Create(), smartOfferId, slotId, serviceDate),
                autoSave: true);
            await uow.CompleteAsync();
        }
        catch (Exception)
        {
            // The only expected failure is the unique (SlotId, ServiceDate) index losing the race. Anything else still
            // matters, so it is rethrown unless the row now exists.
            if (!await InventoryRepository.AnyAsync(i => i.SlotId == slotId && i.ServiceDate == serviceDate))
            {
                throw;
            }
        }
    }

    // Runs stock-touching work in its own unit of work and retries it when another request changed the same stock row
    // first. The work must load everything it reads inside the lambda, so each attempt sees current data.
    protected async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> work)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
                var result = await work();
                await uow.CompleteAsync();
                return result;
            }
            catch (Volo.Abp.Data.AbpDbConcurrencyException) when (attempt < MaxStockAttempts)
            {
                // Another buyer or staff member changed this stock first. Retry against the fresh row.
            }
            catch (Volo.Abp.Data.AbpDbConcurrencyException)
            {
                throw new UserFriendlyException("Lots of customers are ordering this deal right now. Please try again.");
            }
        }
    }

    protected async Task<string> GenerateUniqueOrderCodeAsync()
    {
        for (var attempt = 0; attempt < MaxOrderCodeAttempts; attempt++)
        {
            // Guid.NewGuid rather than the sequential GuidGenerator: the code needs its whole length to be random, and
            // a sequential prefix would make same-millisecond codes collide.
            var code = Guid.NewGuid().ToString("N")[..SmartOfferConsts.CodeLength].ToUpperInvariant();

            // Uniqueness is checked across tenants. The staff counter looks codes up without a tenant filter, so two
            // businesses sharing a code would be ambiguous there.
            using (DataFilter.Disable<IMultiTenant>())
            {
                if (!await OrderRepository.AnyAsync(o => o.Code == code))
                {
                    return code;
                }
            }
        }

        throw new UserFriendlyException("Couldn't create an order code. Please try again.");
    }

    protected SmartOfferOrderDto ToOrderDto(SmartOfferOrder order, DateTime nowUtc)
    {
        var dto = ObjectMapper.Map<SmartOfferOrder, SmartOfferOrderDto>(order);
        dto.ServerNowUtc = nowUtc;

        // Presented as Expired the moment the window closes, even before the background worker has written it. The
        // same rule stops Complete, so the screen can never show "ready" for an order staff will refuse.
        if (order.HasLapsed(nowUtc))
        {
            dto.Status = SmartOfferOrderStatus.Expired;
        }

        return dto;
    }

    protected async Task<(Guid EmployeeId, Guid? BranchId)> CheckStaffRoleAsync(
        IRepository<EmployeeAssignment, Guid> assignments,
        params EmployeeRole[] allowedRoles)
    {
        var userId = CurrentUser.GetId();
        var assignment = await assignments.FirstOrDefaultAsync(a => a.UserId == userId)
            ?? throw new AbpAuthorizationException("You are not staff at this business.");

        if (!allowedRoles.Contains(assignment.Role))
        {
            throw new AbpAuthorizationException("Your role does not permit this action.");
        }

        return (userId, assignment.BranchId);
    }

    // Name from the profile, phone from the identity user. Same split PosAppService uses, since a customer who
    // registered by OTP may have no profile yet.
    protected async Task<(string? Name, string? Phone)> ResolveCustomerIdentityAsync(
        IRepository<CustomerProfile, Guid> profiles,
        IRepository<IdentityUser, Guid> users,
        Guid customerId)
    {
        var profile = await profiles.FirstOrDefaultAsync(p => p.UserId == customerId);
        var user = await users.FirstOrDefaultAsync(u => u.Id == customerId);

        var name = profile == null ? null : $"{profile.FirstName} {profile.LastName}".Trim();
        return (string.IsNullOrWhiteSpace(name) ? null : name, user?.PhoneNumber);
    }
}
