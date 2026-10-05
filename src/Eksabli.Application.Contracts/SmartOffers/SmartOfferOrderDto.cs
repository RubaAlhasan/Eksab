using System;
using Eksabli.Shared;
using Volo.Abp.Application.Dtos;

namespace Eksabli.SmartOffers;

public class SmartOfferOrderDto : AuditedEntityDto<Guid>
{
    public Guid SmartOfferId { get; set; }

    public string OfferTitleAr { get; set; } = string.Empty;

    public string OfferTitleEn { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal BasePrice { get; set; }

    public decimal TotalAmount { get; set; }

    public Currency Currency { get; set; }

    public SmartOfferOrderStatus Status { get; set; }

    public DateOnly ServiceDate { get; set; }

    public DateTime PlacedAt { get; set; }

    public DateTime ReservationExpiresAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? RejectionReason { get; set; }

    // The server's clock at the time of this response. Lets the customer's countdown agree with the server's and
    // not the phone's.
    public DateTime ServerNowUtc { get; set; }
}

// Staff counter view. Adds who is asking for the money, so the cashier can match the person at the counter.
public class SmartOfferOrderStaffDto : SmartOfferOrderDto
{
    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    // The deal's own description, read live from the offer, so the counter can show what was sold.
    public string? OfferDescriptionAr { get; set; }

    public string? OfferDescriptionEn { get; set; }
}
