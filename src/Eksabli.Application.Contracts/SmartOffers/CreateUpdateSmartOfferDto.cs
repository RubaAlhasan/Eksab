using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Eksabli.Shared;

namespace Eksabli.SmartOffers;

public class CreateUpdateSmartOfferDto
{
    [Required]
    [StringLength(SmartOfferConsts.MaxTitleLength)]
    public string TitleAr { get; set; } = string.Empty;

    [Required]
    [StringLength(SmartOfferConsts.MaxTitleLength)]
    public string TitleEn { get; set; } = string.Empty;

    [StringLength(SmartOfferConsts.MaxDescriptionLength)]
    public string? DescriptionAr { get; set; }

    [StringLength(SmartOfferConsts.MaxDescriptionLength)]
    public string? DescriptionEn { get; set; }

    [Required]
    public SmartPricingStrategy Strategy { get; set; }

    [Required]
    public Currency Currency { get; set; }

    [Required]
    public decimal BasePrice { get; set; }

    public decimal? MinimumPrice { get; set; }

    public int? DailyQuantity { get; set; }

    [Required]
    [StringLength(SmartOfferConsts.MaxTimeZoneIdLength)]
    public string TimeZoneId { get; set; } = string.Empty;

    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public bool IsEnabled { get; set; } = true;

    // Empty for a Fixed offer. A TimeBased offer needs at least one. The server validates overlap and ordering
    // again, so these checks are about shape only.
    public List<CreateUpdateSmartOfferStageDto> Stages { get; set; } = new();
}

public class CreateUpdateSmartOfferStageDto
{
    // Null for a new stage. Set for an existing one, so editing keeps the stage's identity (and any pending orders
    // still pointing at it).
    public Guid? Id { get; set; }

    [Required]
    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Use the HH:mm format.")]
    public string StartTime { get; set; } = string.Empty;

    // "24:00" is allowed here, as the end of a stage that runs to midnight.
    [Required]
    [RegularExpression(@"^(([01]\d|2[0-3]):[0-5]\d|24:00)$", ErrorMessage = "Use the HH:mm format.")]
    public string EndTime { get; set; } = string.Empty;

    [Required]
    public decimal Price { get; set; }

    [Required]
    public Currency Currency { get; set; }

    public int? QuantityLimit { get; set; }
}

public class SetSmartOfferEnabledDto
{
    [Required]
    public bool IsEnabled { get; set; }
}
