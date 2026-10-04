using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.BusinessProfiles;
using Eksabli.Engagement;
using Eksabli.Memberships;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace Eksabli.SmartOffers;

// Customer-facing. Every price is quoted and every window enforced here on the server. The customer app sends a
// tenant, an offer and a quantity, and never a price.
[RemoteService(IsEnabled = false)]
public class CustomerSmartOfferAppService : SmartOfferServiceBase, ICustomerSmartOfferAppService
{
    // The browse page filters and pages the feed in the browser, so it needs the whole feed up to this bound, not one page.
    private const int MaxFeedSize = 200;
    private const int MaxOrdersPageSize = 50;
    private const string FallbackBusinessName = "Business";

    private readonly ISmartOfferRepository _offerRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<Follow, Guid> _followRepository;
    private readonly IRepository<BusinessProfile, Guid> _businessProfileRepository;

    public CustomerSmartOfferAppService(
        ISmartOfferRepository offerRepository,
        IRepository<Membership, Guid> membershipRepository,
        IRepository<Follow, Guid> followRepository,
        IRepository<BusinessProfile, Guid> businessProfileRepository,
        IRepository<SmartOfferOrder, Guid> orderRepository,
        IRepository<SmartOfferInventory, Guid> inventoryRepository)
        : base(orderRepository, inventoryRepository)
    {
        _offerRepository = offerRepository;
        _membershipRepository = membershipRepository;
        _followRepository = followRepository;
        _businessProfileRepository = businessProfileRepository;
    }

    public async Task<CustomerSmartOfferListDto> GetOffersAsync(Guid tenantId)
    {
        using (CurrentTenant.Change(tenantId))
        {
            var nowUtc = NowUtc;
            var customerId = CurrentUser.GetId();
            var membership = await _membershipRepository.FirstOrDefaultAsync(m => m.CustomerId == customerId);

            // Only what a customer can act on or is about to: paused and expired deals are hidden, not greyed out.
            // Every enabled deal of this business, then the status filter in memory. A row cap taken before that filter
            // could drop a live deal once enough ended deals were newer than it, so there is no cap here.
            var offers = await _offerRepository.GetEnabledForTenantsAsync(new[] { tenantId });
            var visible = offers
                .Where(o => IsShownToCustomers(o.GetStatus(nowUtc)))
                .OrderByDescending(o => o.CreationTime)
                .ToList();

            var stock = await LoadTodayStockAsync(visible, nowUtc);
            var pending = membership == null
                ? new Dictionary<Guid, SmartOfferOrder>()
                : await LoadPendingByOfferAsync(new[] { membership.Id }, nowUtc);

            var result = new CustomerSmartOfferListDto();
            foreach (var offer in visible)
            {
                result.Items.Add(ToOfferDto(offer, nowUtc, stock, pending, isMember: membership != null, businessName: null));
            }

            return result;
        }
    }

    public async Task<CustomerSmartOfferListDto> GetFeedAsync(int maxResultCount = 30)
    {
        var customerId = CurrentUser.GetId();
        var nowUtc = NowUtc;
        var take = Math.Clamp(maxResultCount, 1, MaxFeedSize);

        // Same business selection as the campaign feed (CustomerCampaignAppService.GetMyFeedAsync): businesses the
        // customer has joined, plus businesses they follow, and only approved ones. A deal from a business that is
        // pending or suspended is never shown.
        using (DataFilter.Disable<IMultiTenant>())
        {
            var memberships = (await _membershipRepository.GetListAsync(m => m.CustomerId == customerId && m.Status == MembershipStatus.Active))
                .Where(m => m.TenantId.HasValue)
                .ToList();
            var follows = await _followRepository.GetListAsync(f => f.CustomerId == customerId);

            var tenantIds = memberships.Select(m => m.TenantId!.Value)
                .Union(follows.Where(f => f.TenantId.HasValue).Select(f => f.TenantId!.Value))
                .Distinct()
                .ToList();
            if (tenantIds.Count == 0)
            {
                return new CustomerSmartOfferListDto();
            }

            var profiles = (await _businessProfileRepository.GetListAsync(p => p.TenantId != null && tenantIds.Contains(p.TenantId.Value)))
                .Where(p => p.TenantId.HasValue && p.ApprovalStatus == TenantApprovalStatus.Approved)
                .ToList();
            if (profiles.Count == 0)
            {
                return new CustomerSmartOfferListDto();
            }

            var approvedTenantIds = profiles.Select(p => p.TenantId!.Value).ToList();
            var businessNames = profiles.ToDictionary(
                p => p.TenantId!.Value,
                p => string.IsNullOrWhiteSpace(p.DisplayName) ? FallbackBusinessName : p.DisplayName!);
            var memberTenantIds = memberships.Select(m => m.TenantId!.Value).ToHashSet();

            var candidates = (await _offerRepository.GetEnabledForTenantsAsync(approvedTenantIds))
                .Where(o => IsShownToCustomers(o.GetStatus(nowUtc)))
                .ToList();

            var stock = await LoadTodayStockAsync(candidates, nowUtc);
            var pending = await LoadPendingByOfferAsync(memberships.Select(m => m.Id).ToList(), nowUtc);

            var items = candidates
                .Select(offer => ToOfferDto(
                    offer,
                    nowUtc,
                    stock,
                    pending,
                    isMember: memberTenantIds.Contains(offer.TenantId!.Value),
                    businessName: businessNames[offer.TenantId!.Value]))
                // Live first, then the deal that ends soonest: the order a shopper scanning the feed wants.
                .OrderByDescending(d => d.IsAvailableNow)
                .ThenBy(d => d.CurrentStageEndsAtUtc ?? DateTime.MaxValue)
                .ThenBy(d => d.NextChangeAtUtc ?? DateTime.MaxValue)
                .Take(take)
                .ToList();

            return new CustomerSmartOfferListDto { Items = items };
        }
    }

    public async Task<PagedResultDto<CustomerSmartOfferOrderDto>> GetMyOrdersAsync(GetMySmartOfferOrdersInput input)
    {
        var customerId = CurrentUser.GetId();
        var nowUtc = NowUtc;

        // Every membership counts, active or not: a customer who has left a business keeps their history there.
        using (DataFilter.Disable<IMultiTenant>())
        {
            var membershipIds = (await _membershipRepository.GetListAsync(m => m.CustomerId == customerId))
                .Select(m => m.Id)
                .ToList();
            if (membershipIds.Count == 0)
            {
                return new PagedResultDto<CustomerSmartOfferOrderDto>(0, new List<CustomerSmartOfferOrderDto>());
            }

            var query = (await OrderRepository.GetQueryableAsync())
                .Where(o => membershipIds.Contains(o.MembershipId));
            query = ApplyOrderFilter(query, input.Filter, nowUtc);

            var total = await AsyncExecuter.CountAsync(query);
            var orders = await AsyncExecuter.ToListAsync(
                query
                    .OrderByDescending(o => o.CreationTime)
                    .Skip(input.SkipCount)
                    .Take(Math.Clamp(input.MaxResultCount, 1, MaxOrdersPageSize)));

            var businessNames = await LoadBusinessNamesAsync(orders.Where(o => o.TenantId.HasValue).Select(o => o.TenantId!.Value));

            // One query for the page's deals, so each order can show its deal's description.
            var offerIds = orders.Select(o => o.SmartOfferId).Distinct().ToList();
            var offersById = offerIds.Count == 0
                ? new Dictionary<Guid, SmartOffer>()
                : (await _offerRepository.GetListAsync(o => offerIds.Contains(o.Id))).ToDictionary(o => o.Id);

            var items = orders.Select(order =>
            {
                var dto = ObjectMapper.Map<SmartOfferOrder, CustomerSmartOfferOrderDto>(order);
                dto.Status = order.HasLapsed(nowUtc) ? SmartOfferOrderStatus.Expired : order.Status;
                dto.ServerNowUtc = nowUtc;
                dto.BusinessName = order.TenantId.HasValue && businessNames.TryGetValue(order.TenantId.Value, out var name)
                    ? name
                    : null;
                if (offersById.TryGetValue(order.SmartOfferId, out var offer))
                {
                    dto.OfferDescriptionAr = offer.DescriptionAr;
                    dto.OfferDescriptionEn = offer.DescriptionEn;
                }
                return dto;
            }).ToList();

            return new PagedResultDto<CustomerSmartOfferOrderDto>(total, items);
        }
    }

    public async Task<CustomerSmartOfferDetailsDto> GetOfferDetailsAsync(Guid tenantId, Guid offerId)
    {
        var customerId = CurrentUser.GetId();

        using (CurrentTenant.Change(tenantId))
        {
            // Any membership counts, active or not, as in GetMyOrdersAsync. Without one there is no order to check.
            var membership = await _membershipRepository.FirstOrDefaultAsync(m => m.CustomerId == customerId)
                ?? throw new EntityNotFoundException(typeof(SmartOffer), offerId);

            // A customer can open only a deal they have ordered here. Anything else reads as "not found", so the
            // endpoint never confirms that some other deal exists.
            var hasOrdered = await AsyncExecuter.AnyAsync(
                (await OrderRepository.GetQueryableAsync()).Where(o => o.MembershipId == membership.Id && o.SmartOfferId == offerId));
            if (!hasOrdered)
            {
                throw new EntityNotFoundException(typeof(SmartOffer), offerId);
            }

            var offer = await _offerRepository.FindAsync(offerId)
                ?? throw new EntityNotFoundException(typeof(SmartOffer), offerId);

            var businessNames = await LoadBusinessNamesAsync(new[] { tenantId });

            return new CustomerSmartOfferDetailsDto
            {
                Id = offer.Id,
                TenantId = tenantId,
                BusinessName = businessNames.TryGetValue(tenantId, out var name) ? name : FallbackBusinessName,
                TitleAr = offer.TitleAr,
                TitleEn = offer.TitleEn,
                DescriptionAr = offer.DescriptionAr,
                DescriptionEn = offer.DescriptionEn,
                Currency = offer.Currency,
                BasePrice = offer.BasePrice,
            };
        }
    }

    public async Task<SmartOfferOrderDto> PlaceOrderAsync(PlaceSmartOfferOrderDto input)
    {
        var customerId = CurrentUser.GetId();

        using (CurrentTenant.Change(input.TenantId))
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                // Active only: starting a new order needs a current membership, as a new redemption does.
                var membership = await _membershipRepository.FirstOrDefaultAsync(
                        m => m.CustomerId == customerId && m.Status == MembershipStatus.Active)
                    ?? throw new UserFriendlyException("You haven't joined this business yet.");

                var offer = await _offerRepository.FindWithStagesAsync(input.SmartOfferId)
                    ?? throw new UserFriendlyException("This deal is no longer available.");

                var nowUtc = NowUtc;

                // The price is quoted here, now, and nowhere else. Anything the customer saw on screen was only a hint.
                var quote = offer.Quote(nowUtc)
                    ?? throw new UserFriendlyException("This deal isn't on sale right now.");

                // One open order per customer per deal. A double-tap or a return to the screen gets back the order the
                // customer already has. A different quantity is refused rather than silently ignored, so the customer is
                // never shown a hold for a number they did not ask for.
                var existing = await OrderRepository.FirstOrDefaultAsync(o =>
                    o.MembershipId == membership.Id &&
                    o.SmartOfferId == offer.Id &&
                    o.Status == SmartOfferOrderStatus.Pending &&
                    o.ReservationExpiresAt > nowUtc);

                if (existing != null)
                {
                    if (existing.Quantity != input.Quantity)
                    {
                        throw new UserFriendlyException(
                            $"You already have {existing.Quantity} of this deal on hold. Cancel that order before ordering a different quantity.");
                    }

                    return ToOrderDto(existing, nowUtc);
                }

                var (serviceDate, _) = offer.ToLocal(nowUtc);
                await EnsureInventoryAsync(offer.Id, quote.SlotId, serviceDate);

                var inventory = await InventoryRepository.FirstAsync(i => i.SlotId == quote.SlotId && i.ServiceDate == serviceDate);
                inventory.Hold(input.Quantity, quote.QuantityLimit);
                await InventoryRepository.UpdateAsync(inventory, autoSave: true);

                var order = SmartOfferOrder.Place(
                    GuidGenerator.Create(),
                    offer.Id,
                    membership.Id,
                    await GenerateUniqueOrderCodeAsync(),
                    offer.TitleAr,
                    offer.TitleEn,
                    quote,
                    input.Quantity,
                    serviceDate,
                    nowUtc);

                await OrderRepository.InsertAsync(order, autoSave: true);
                return ToOrderDto(order, nowUtc);
            });
        }
    }

    public async Task<SmartOfferOrderDto> GetMyOrderAsync(Guid tenantId, Guid orderId)
    {
        using (CurrentTenant.Change(tenantId))
        {
            var nowUtc = NowUtc;
            var order = await GetOwnOrderAsync(orderId);
            return ToOrderDto(order, nowUtc);
        }
    }

    public async Task<SmartOfferOrderDto> CancelMyOrderAsync(Guid tenantId, Guid orderId)
    {
        using (CurrentTenant.Change(tenantId))
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var nowUtc = NowUtc;
                var order = await GetOwnOrderAsync(orderId);

                order.Cancel();
                await OrderRepository.UpdateAsync(order, autoSave: true);
                await ReleaseStockAsync(order);

                return ToOrderDto(order, nowUtc);
            });
        }
    }

    // Loads an order only if it belongs to the caller. Ownership goes through Membership rather than trusting the id: the
    // tenant filter scopes the row to one business, not to one customer within it.
    private async Task<SmartOfferOrder> GetOwnOrderAsync(Guid orderId)
    {
        var customerId = CurrentUser.GetId();
        var membership = await _membershipRepository.FirstOrDefaultAsync(m => m.CustomerId == customerId)
            ?? throw new UserFriendlyException("You haven't joined this business yet.");

        return await OrderRepository.FirstOrDefaultAsync(o => o.Id == orderId && o.MembershipId == membership.Id)
            ?? throw new EntityNotFoundException(typeof(SmartOfferOrder), orderId);
    }

    private async Task ReleaseStockAsync(SmartOfferOrder order)
    {
        var inventory = await InventoryRepository.FirstOrDefaultAsync(i => i.SlotId == order.SlotId && i.ServiceDate == order.ServiceDate);
        if (inventory != null)
        {
            inventory.Release(order.Quantity);
            await InventoryRepository.UpdateAsync(inventory, autoSave: true);
        }
    }

    // Live, between stages, or scheduled: the three states a customer should see. Paused and ended deals are hidden.
    private static bool IsShownToCustomers(SmartOfferStatus status) =>
        status is SmartOfferStatus.Live or SmartOfferStatus.BetweenStages or SmartOfferStatus.Scheduled;

    private static IQueryable<SmartOfferOrder> ApplyOrderFilter(IQueryable<SmartOfferOrder> query, CustomerDealOrderFilter filter, DateTime nowUtc) =>
        filter switch
        {
            CustomerDealOrderFilter.Active => query.Where(o => o.Status == SmartOfferOrderStatus.Pending && o.ReservationExpiresAt > nowUtc),
            CustomerDealOrderFilter.Completed => query.Where(o => o.Status == SmartOfferOrderStatus.Completed),
            CustomerDealOrderFilter.Closed => query.Where(o =>
                o.Status == SmartOfferOrderStatus.Cancelled ||
                o.Status == SmartOfferOrderStatus.Rejected ||
                o.Status == SmartOfferOrderStatus.Expired ||
                (o.Status == SmartOfferOrderStatus.Pending && o.ReservationExpiresAt <= nowUtc)),
            _ => query,
        };

    // Stock for every slot the given offers can sell today, loaded in one query. Each offer's "today" is its own local
    // date, so the lookup is keyed on (slot, date) and never assumes one date for the whole batch.
    private async Task<Dictionary<(Guid SlotId, DateOnly ServiceDate), SmartOfferInventory>> LoadTodayStockAsync(
        IReadOnlyCollection<SmartOffer> offers,
        DateTime nowUtc)
    {
        if (offers.Count == 0)
        {
            return new Dictionary<(Guid SlotId, DateOnly ServiceDate), SmartOfferInventory>();
        }

        var days = offers.Select(o => o.ToLocal(nowUtc).Date).ToList();
        var from = days.Min();
        var to = days.Max();
        var slotIds = offers.SelectMany(SlotIdsOf).Distinct().ToList();

        var rows = await InventoryRepository.GetListAsync(i =>
            slotIds.Contains(i.SlotId) && i.ServiceDate >= from && i.ServiceDate <= to);

        return rows.ToDictionary(i => (i.SlotId, i.ServiceDate));
    }

    private static IEnumerable<Guid> SlotIdsOf(SmartOffer offer) =>
        offer.Strategy == SmartPricingStrategy.Fixed
            ? new[] { offer.Id }
            : offer.Stages.Select(stage => stage.Id);

    // The caller's open reservations, one per deal (the newest, if a legacy duplicate ever exists).
    private async Task<Dictionary<Guid, SmartOfferOrder>> LoadPendingByOfferAsync(IReadOnlyCollection<Guid> membershipIds, DateTime nowUtc)
    {
        if (membershipIds.Count == 0)
        {
            return new Dictionary<Guid, SmartOfferOrder>();
        }

        var ids = membershipIds.ToList();
        var orders = await OrderRepository.GetListAsync(o =>
            ids.Contains(o.MembershipId) && o.Status == SmartOfferOrderStatus.Pending && o.ReservationExpiresAt > nowUtc);

        return orders
            .GroupBy(o => o.SmartOfferId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(o => o.CreationTime).First());
    }

    private async Task<Dictionary<Guid, string>> LoadBusinessNamesAsync(IEnumerable<Guid> tenantIds)
    {
        var ids = tenantIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        using (DataFilter.Disable<IMultiTenant>())
        {
            var profiles = await _businessProfileRepository.GetListAsync(p => p.TenantId != null && ids.Contains(p.TenantId.Value));
            return profiles
                .Where(p => p.TenantId.HasValue)
                .ToDictionary(
                    p => p.TenantId!.Value,
                    p => string.IsNullOrWhiteSpace(p.DisplayName) ? FallbackBusinessName : p.DisplayName!);
        }
    }

    private CustomerSmartOfferDto ToOfferDto(
        SmartOffer offer,
        DateTime nowUtc,
        IReadOnlyDictionary<(Guid SlotId, DateOnly ServiceDate), SmartOfferInventory> stock,
        IReadOnlyDictionary<Guid, SmartOfferOrder> pendingByOffer,
        bool isMember,
        string? businessName)
    {
        var quote = offer.Quote(nowUtc);
        var nextChange = offer.GetNextChange(nowUtc);
        var (today, _) = offer.ToLocal(nowUtc);

        var slotStock = quote == null
            ? null
            : stock.GetValueOrDefault((quote.SlotId, today));

        var stage = quote?.StageId == null ? null : offer.Stages.FirstOrDefault(s => s.Id == quote.StageId);

        var dto = new CustomerSmartOfferDto
        {
            Id = offer.Id,
            TenantId = offer.TenantId,
            TitleAr = offer.TitleAr,
            TitleEn = offer.TitleEn,
            DescriptionAr = offer.DescriptionAr,
            DescriptionEn = offer.DescriptionEn,
            Strategy = offer.Strategy,
            Currency = offer.Currency,
            BasePrice = offer.BasePrice,
            Status = offer.GetStatus(nowUtc),
            IsAvailableNow = quote != null,
            CurrentPrice = quote?.Price,
            DiscountPercent = quote != null ? DiscountPercent(offer.BasePrice, quote.Price) : null,
            CurrentStageStartTime = stage == null ? null : MinuteOfDay.Format(stage.StartMinute),
            CurrentStageEndTime = stage == null ? null : MinuteOfDay.Format(stage.EndMinute),
            CurrentStageEndsAtUtc = quote?.WindowEndUtc,
            RemainingNow = quote == null ? null : RemainingFor(slotStock, quote.QuantityLimit),
            MaxOrderQuantity = SmartOfferConsts.MaxOrderQuantity,
            NextChangeAtUtc = nextChange?.AtUtc,
            NextPrice = nextChange?.Price,
            ValidTo = offer.ValidTo,
            ServerNowUtc = nowUtc,
            BusinessName = businessName,
            IsMember = isMember,
            MyPendingOrder = pendingByOffer.TryGetValue(offer.Id, out var pending) ? ToOrderDto(pending, nowUtc) : null,
        };

        if (nextChange != null)
        {
            var (nextDate, nextMinute) = offer.ToLocal(nextChange.AtUtc);
            dto.NextChangeLocalTime = MinuteOfDay.Format(nextMinute);
            dto.NextChangeIsTomorrow = nextDate > today;
        }

        return dto;
    }

    // Rounded half-up, whole percent. Zero when there is no real discount, so a deal at its base price shows no badge.
    private static int? DiscountPercent(decimal basePrice, decimal price)
    {
        if (price >= basePrice)
        {
            return null;
        }

        var percent = (basePrice - price) / basePrice * 100m;
        return (int)Math.Round(percent, 0, MidpointRounding.AwayFromZero);
    }
}
