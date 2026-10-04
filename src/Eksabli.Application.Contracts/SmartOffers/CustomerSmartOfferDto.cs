using System;
using System.Collections.Generic;
using Eksabli.Shared;
using Volo.Abp.Application.Dtos;

namespace Eksabli.SmartOffers;

// Customer-facing view. Every price here was computed on the server at ServerNowUtc. The client only formats it and
// counts down; it never works out a price, a discount, or a window itself.
public class CustomerSmartOfferDto : EntityDto<Guid>
{
    public Guid? TenantId { get; set; }

    public string TitleAr { get; set; } = string.Empty;

    public string TitleEn { get; set; } = string.Empty;

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public SmartPricingStrategy Strategy { get; set; }

    public Currency Currency { get; set; }

    public decimal BasePrice { get; set; }

    public SmartOfferStatus Status { get; set; }

    // True when a price applies right now and the customer can buy it.
    public bool IsAvailableNow { get; set; }

    public decimal? CurrentPrice { get; set; }

    // Whole-number percentage off BasePrice, rounded half-up. Null when there is no discount to show (price equals
    // the base, or nothing is on sale).
    public int? DiscountPercent { get; set; }

    // The stage the current price comes from, in the restaurant's local clock. Null for a fixed-price offer.
    public string? CurrentStageStartTime { get; set; }

    public string? CurrentStageEndTime { get; set; }

    public DateTime? CurrentStageEndsAtUtc { get; set; }

    public int? RemainingNow { get; set; }

    public int MaxOrderQuantity { get; set; }

    public DateTime? NextChangeAtUtc { get; set; }

    // The next change in the restaurant's local clock, e.g. "15:00", for the "price drops at 15:00" line.
    public string? NextChangeLocalTime { get; set; }

    public bool NextChangeIsTomorrow { get; set; }

    public decimal? NextPrice { get; set; }

    public DateOnly? ValidTo { get; set; }

    public DateTime ServerNowUtc { get; set; }

    // The business's own display name, so a feed of deals from several businesses says where each one is.
    public string? BusinessName { get; set; }

    // Whether the caller holds an active membership at this business. Only members can buy; anyone else sees the
    // deal and is told to join.
    public bool IsMember { get; set; }

    // The caller's own open reservation for this offer, if any, so the card can show the code without a second call.
    public SmartOfferOrderDto? MyPendingOrder { get; set; }
}

public class CustomerSmartOfferListDto
{
    public List<CustomerSmartOfferDto> Items { get; set; } = new();
}
