using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.SmartOffers;

// Stock for one slot on one local service day. A slot is a stage (time-based) or the offer itself (fixed), so a
// stage's quantity resets each day with no reset job.
//
// Capacity is deliberately not stored here. The quota lives on the stage or offer, which the owner can edit
// mid-day, and Hold takes the current capacity as an argument. Storing a snapshot would leave two numbers that
// could disagree.
//
// Concurrency: this row is the contended resource (every buyer of the same slot writes to it). It is an
// AggregateRoot, so ABP's concurrency stamp makes a conflicting write fail rather than silently overselling. The
// application layer retries on that conflict.
public class SmartOfferInventory : AggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid SmartOfferId { get; private set; }

    public Guid SlotId { get; private set; }

    public DateOnly ServiceDate { get; private set; }

    // Held by Pending orders. Still available to sell, but already promised to a customer.
    public int Reserved { get; private set; }

    // Confirmed at the counter. Gone for good.
    public int Sold { get; private set; }

    protected SmartOfferInventory()
    {
        /* Required by the ORM */
    }

    private SmartOfferInventory(Guid id, Guid smartOfferId, Guid slotId, DateOnly serviceDate)
        : base(id)
    {
        SmartOfferId = smartOfferId;
        SlotId = slotId;
        ServiceDate = serviceDate;
    }

    public static SmartOfferInventory Create(Guid id, Guid smartOfferId, Guid slotId, DateOnly serviceDate) =>
        new(id, smartOfferId, slotId, serviceDate);

    // Null capacity means unlimited, so there is no "remaining" to report.
    public int? Remaining(int? capacity) =>
        capacity.HasValue ? Math.Max(0, capacity.Value - Reserved - Sold) : null;

    public void Hold(int quantity, int? capacity)
    {
        GuardQuantity(quantity);

        var remaining = Remaining(capacity);
        if (remaining.HasValue && remaining.Value < quantity)
        {
            throw new UserFriendlyException(
                remaining.Value == 0
                    ? "This deal is sold out for now."
                    : $"Only {remaining.Value} left at this price. Choose a smaller quantity.");
        }

        Reserved += quantity;
    }

    // A reservation that didn't complete (cancelled, rejected, expired) gives its units back.
    public void Release(int quantity)
    {
        GuardQuantity(quantity);
        if (Reserved < quantity)
        {
            // A double release would hand out units that were never held. Reaching this means a caller skipped
            // the order state machine.
            throw new AbpException($"Inventory {Id} holds {Reserved} reserved units; cannot release {quantity}.");
        }

        Reserved -= quantity;
    }

    public void Confirm(int quantity)
    {
        GuardQuantity(quantity);
        if (Reserved < quantity)
        {
            throw new AbpException($"Inventory {Id} holds {Reserved} reserved units; cannot confirm {quantity}.");
        }

        Reserved -= quantity;
        Sold += quantity;
    }

    private static void GuardQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "A stock movement must be positive.");
        }
    }
}
