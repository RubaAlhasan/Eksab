using System;

namespace Eksabli.Pos;

// What staff see after entering or scanning a code, BEFORE they approve anything.
//
// This exists because approval is a judgement call, not a formality: the cashier has to confirm the
// person in front of them is the member, that the reward is one the branch can actually hand over, and
// — for high-value rewards — that they are allowed to approve it at all. Firing straight from "code
// entered" to "points debited" gives them nothing to check against.
public class RedemptionLookupDto
{
    public Guid CouponId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string? RewardNameAr { get; set; }

    public string? RewardNameEn { get; set; }

    public int PointsCost { get; set; }

    // Who is standing at the counter. Null where the customer never completed a profile — the UI
    // falls back to the phone number rather than showing a blank card.
    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    // Balance the customer is left with if this is approved. Already net of this reservation, so it is
    // the number to show as "after redemption" without further arithmetic.
    public int BalanceAfterRedemption { get; set; }

    public DateTime IssuedAt { get; set; }

    public DateTime? ReservationExpiresAt { get; set; }

    // True when the reward's own ApprovalThresholdPoints puts it above a cashier's authority, so the
    // UI can say "needs a manager" up front instead of letting them press Approve and eat a 403.
    public bool RequiresManagerApproval { get; set; }

    // Whether the CURRENT caller may approve this specific coupon — the threshold check above resolved
    // against their own EmployeeAssignment.Role.
    public bool CanCurrentEmployeeApprove { get; set; }
}
