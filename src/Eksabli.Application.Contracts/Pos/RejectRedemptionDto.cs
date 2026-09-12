using System.ComponentModel.DataAnnotations;
using Eksabli.Rewards;

namespace Eksabli.Pos;

public class RejectRedemptionDto
{
    [Required]
    [StringLength(CouponConsts.MaxSubmittedCodeLength)]
    public string Code { get; set; } = string.Empty;

    // Optional and free-text: the useful reasons ("we're out of oat milk", "customer left") are not a
    // fixed set, and forcing staff to pick from a dropdown they don't recognise produces noise, not
    // data. Surfaced back to the customer, so it is shown to them verbatim.
    [StringLength(CouponConsts.MaxRejectionReasonLength)]
    public string? Reason { get; set; }
}
