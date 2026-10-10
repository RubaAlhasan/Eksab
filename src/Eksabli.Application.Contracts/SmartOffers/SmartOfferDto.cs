using System;
using System.Collections.Generic;
using Eksabli.Shared;
using Volo.Abp.Application.Dtos;

namespace Eksabli.SmartOffers;

// Owner-facing view. Carries the live state too (current price, what's left right now, next change) so the Business
// Portal shows what customers see without a second round-trip, and without the client recomputing any of it.
public class SmartOfferDto : FullAuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }

    public string TitleAr { get; set; } = string.Empty;

    public string TitleEn { get; set; } = string.Empty;

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public SmartPricingStrategy Strategy { get; set; }

    public Currency Currency { get; set; }

    public decimal BasePrice { get; set; }

    public decimal? MinimumPrice { get; set; }

    public int? DailyQuantity { get; set; }

    public string TimeZoneId { get; set; } = string.Empty;

    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public bool IsEnabled { get; set; }

    public SmartOfferStatus Status { get; set; }

    public List<SmartOfferStageDto> Stages { get; set; } = new();

    // What a customer would pay at ServerNowUtc. Null when nothing is on sale.
    public decimal? CurrentPrice { get; set; }

    // The stage that sets CurrentPrice, so the owner's timeline highlights the right one. Null for fixed pricing or when
    // nothing is on sale.
    public Guid? CurrentStageId { get; set; }

    // Stock left for the current slot today (per stage for time-based, per day for fixed). Null means unlimited.
    public int? RemainingNow { get; set; }

    public DateTime ServerNowUtc { get; set; }

    // When the price next changes, and to what. A null NextPrice with a value set means "goes off sale".
    public DateTime? NextChangeAtUtc { get; set; }

    public decimal? NextPrice { get; set; }
}

public class SmartOfferStageDto : EntityDto<Guid>
{
    public string StartTime { get; set; } = string.Empty;

    public string EndTime { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public Currency Currency { get; set; }

    public int? QuantityLimit { get; set; }

    // Stock left for this stage on the current local day. Null when the stage has no quantity limit.
    public int? RemainingToday { get; set; }
}
