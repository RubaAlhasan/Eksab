using System;

namespace Eksabli.Pos;

public class RedemptionRejectionDto
{
    public Guid CouponId { get; set; }

    public string? RewardNameAr { get; set; }

    public string? RewardNameEn { get; set; }

    // Points handed back to the customer — the whole point of rejecting rather than letting the
    // reservation rot until the worker sweeps it.
    public int PointsReleased { get; set; }

    public int NewAvailableBalance { get; set; }
}
