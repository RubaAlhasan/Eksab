using System;

namespace Eksabli.Rewards;

public class CouponExcelDto
{
    public string Code { get; set; } = string.Empty;

    public string RewardNameEn { get; set; } = string.Empty;

    public CouponStatus Status { get; set; }

    public DateTime IssuedAt { get; set; }

    public DateTime? RedeemedAt { get; set; }

    public string? RedeemedByEmployeeEmail { get; set; }

    public string? RedeemedBranchName { get; set; }
}
