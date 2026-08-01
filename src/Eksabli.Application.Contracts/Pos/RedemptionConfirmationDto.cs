using System;

namespace Eksabli.Pos;

public class RedemptionConfirmationDto
{
    public Guid CouponId { get; set; }

    public string? RewardNameAr { get; set; }

    public string? RewardNameEn { get; set; }

    public DateTime RedeemedAt { get; set; }
}
