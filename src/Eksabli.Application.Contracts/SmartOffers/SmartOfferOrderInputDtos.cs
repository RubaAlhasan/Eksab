using System;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.SmartOffers;

public class PlaceSmartOfferOrderDto
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid SmartOfferId { get; set; }

    [Range(1, SmartOfferConsts.MaxOrderQuantity)]
    public int Quantity { get; set; } = 1;
}

public class SmartOfferOrderCodeDto
{
    // Staff type the code the way the customer app shows it, so the same looser inbound length as Coupons applies.
    [Required]
    [StringLength(SmartOfferConsts.CodeLength + 4)]
    public string Code { get; set; } = string.Empty;
}

public class RejectSmartOfferOrderDto : SmartOfferOrderCodeDto
{
    [StringLength(SmartOfferConsts.MaxRejectionReasonLength)]
    public string? Reason { get; set; }
}
