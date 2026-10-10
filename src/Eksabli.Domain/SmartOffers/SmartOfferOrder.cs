using System;
using Eksabli.Shared;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.SmartOffers;

// A customer's Buy Now. The customer decides at home and pays at the counter, so the order is a reservation until
// staff complete it (see SmartOfferOrderStatus). It snapshots the quote it was placed at: the price, currency and
// base price a customer saw are the ones they are charged, whatever the owner changes afterwards.
//
// The reservation can never outlive the price window it was quoted in (see ReservationDeadline). A customer who
// orders at 12:55 for a stage that ends at 13:00 cannot be charged the 11:00–13:00 price at 13:20.
public class SmartOfferOrder : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid SmartOfferId { get; private set; }

    public Guid SlotId { get; private set; }

    public Guid MembershipId { get; private set; }

    public string Code { get; private set; }

    public string OfferTitleAr { get; private set; }

    public string OfferTitleEn { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal BasePrice { get; private set; }

    public decimal TotalAmount { get; private set; }

    public Currency Currency { get; private set; }

    // The business-local date the stock was held against. Needed to find the right inventory row on release, even
    // after midnight has passed.
    public DateOnly ServiceDate { get; private set; }

    public SmartOfferOrderStatus Status { get; private set; }

    public DateTime PlacedAt { get; private set; }

    public DateTime ReservationExpiresAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public Guid? CompletedByEmployeeId { get; private set; }

    public Guid? CompletedBranchId { get; private set; }

    public string? RejectionReason { get; private set; }

    protected SmartOfferOrder()
    {
        Code = string.Empty;
        OfferTitleAr = string.Empty;
        OfferTitleEn = string.Empty;
    }

    private SmartOfferOrder(
        Guid id,
        Guid smartOfferId,
        Guid membershipId,
        string code,
        string offerTitleAr,
        string offerTitleEn,
        SmartPriceQuote quote,
        int quantity,
        DateOnly serviceDate,
        DateTime placedAt,
        DateTime reservationExpiresAt)
        : base(id)
    {
        SmartOfferId = smartOfferId;
        SlotId = quote.SlotId;
        MembershipId = membershipId;
        Code = code;
        OfferTitleAr = offerTitleAr;
        OfferTitleEn = offerTitleEn;
        Quantity = quantity;
        UnitPrice = quote.Price;
        BasePrice = quote.BasePrice;
        TotalAmount = quote.Price * quantity;
        Currency = quote.Currency;
        ServiceDate = serviceDate;
        Status = SmartOfferOrderStatus.Pending;
        PlacedAt = placedAt;
        ReservationExpiresAt = reservationExpiresAt;
    }

    public static SmartOfferOrder Place(
        Guid id,
        Guid smartOfferId,
        Guid membershipId,
        string code,
        string offerTitleAr,
        string offerTitleEn,
        SmartPriceQuote quote,
        int quantity,
        DateOnly serviceDate,
        DateTime placedAt)
    {
        if (quantity < 1 || quantity > SmartOfferConsts.MaxOrderQuantity)
        {
            throw new UserFriendlyException($"You can order between 1 and {SmartOfferConsts.MaxOrderQuantity} at a time.");
        }

        return new SmartOfferOrder(
            id,
            smartOfferId,
            membershipId,
            code,
            offerTitleAr,
            offerTitleEn,
            quote,
            quantity,
            serviceDate,
            placedAt,
            ReservationDeadline(placedAt, quote));
    }

    // The earlier of the standard hold window and the moment the quoted price stops being valid.
    public static DateTime ReservationDeadline(DateTime placedAt, SmartPriceQuote quote)
    {
        var standard = placedAt.AddMinutes(SmartOfferConsts.ReservationWindowMinutes);
        return quote.WindowEndUtc < standard ? quote.WindowEndUtc : standard;
    }

    public bool HoldsStock => Status == SmartOfferOrderStatus.Pending;

    public bool HasLapsed(DateTime nowUtc) => Status == SmartOfferOrderStatus.Pending && ReservationExpiresAt <= nowUtc;

    public void Complete(DateTime nowUtc, Guid employeeId, Guid? branchId)
    {
        if (Status != SmartOfferOrderStatus.Pending)
        {
            throw new UserFriendlyException("This order is no longer awaiting pickup.");
        }

        if (HasLapsed(nowUtc))
        {
            // Checked here, not left to the worker. The worker runs every five minutes, so a lapsed order can still
            // look Pending. Honoring it would mean charging a price that is no longer on offer.
            throw new UserFriendlyException("This order's price has ended. Ask the customer to order again.");
        }

        Status = SmartOfferOrderStatus.Completed;
        CompletedAt = nowUtc;
        CompletedByEmployeeId = employeeId;
        CompletedBranchId = branchId;
    }

    public void Reject(Guid employeeId, string? reason)
    {
        if (Status != SmartOfferOrderStatus.Pending)
        {
            throw new UserFriendlyException("This order is no longer awaiting pickup.");
        }

        Status = SmartOfferOrderStatus.Rejected;
        CompletedByEmployeeId = employeeId;
        RejectionReason = reason;
    }

    public void Cancel()
    {
        if (Status != SmartOfferOrderStatus.Pending)
        {
            throw new UserFriendlyException("This order is no longer awaiting pickup.");
        }

        Status = SmartOfferOrderStatus.Cancelled;
    }

    public void MarkExpired()
    {
        if (Status == SmartOfferOrderStatus.Pending)
        {
            Status = SmartOfferOrderStatus.Expired;
        }
    }
}
