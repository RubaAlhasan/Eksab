using System;
using System.ComponentModel.DataAnnotations;
using Eksabli.Rewards;

namespace Eksabli.Pos;

public class ConfirmRedemptionDto
{
    [Required]
    [StringLength(CouponConsts.CodeLength)]
    public string Code { get; set; } = string.Empty;

    // Defaults to the confirming employee's own EmployeeAssignment.BranchId when omitted.
    public Guid? BranchId { get; set; }
}
