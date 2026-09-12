using System;

namespace Eksabli.Pos;

public class RedemptionConfirmationDto
{
    public Guid CouponId { get; set; }

    public string? RewardNameAr { get; set; }

    public string? RewardNameEn { get; set; }

    public DateTime RedeemedAt { get; set; }

    // Points actually debited by THIS approval. Zero for a legacy `Issued` coupon, whose points were
    // taken when it was created — the UI must not tell staff it just charged a customer twice.
    public int PointsDebited { get; set; }

    public int NewBalance { get; set; }

    public string? CustomerName { get; set; }
}
