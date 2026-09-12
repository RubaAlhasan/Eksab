using System.ComponentModel.DataAnnotations;
using Eksabli.Rewards;

namespace Eksabli.Pos;

public class LookupRedemptionDto
{
    [Required]
    [StringLength(CouponConsts.MaxSubmittedCodeLength)]
    public string Code { get; set; } = string.Empty;
}
