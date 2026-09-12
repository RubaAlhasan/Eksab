using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Rewards;

// Redemption "token" is this row itself, not a separate cache token — unlike a downloaded file, a
// redemption needs a permanent audit trail (RedeemedByEmployeeId/RedeemedBranchId/RedeemedAt), which a
// burned cache entry can't carry. Code doubles as both the QR payload and the typed PIN fallback.
//
// Lifecycle (reservation flow):
//
//     customer taps redeem            staff approve            -> Redeemed   (points debited here)
//     -> Pending (points reserved) -- staff reject             -> Cancelled  (reservation released)
//                                  \_ window closes, worker    -> Expired    (reservation released)
//
// `PointsCost` is snapshotted at creation rather than read back off the Reward at approval time: a
// merchant editing a reward's price must never change what an already-quoted redemption costs, and the
// release path needs to know exactly how much to give back even if the Reward row is gone.
public class Coupon : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid RewardId { get; private set; }

    public Guid MembershipId { get; private set; }

    public Guid? TenantId { get; private set; }

    public string Code { get; private set; }

    public CouponStatus Status { get; private set; }

    // Points held (Pending) or debited (Redeemed) for this coupon. Zero for legacy `Issued` rows
    // created before reservation existed — see CouponStatus's comment.
    public int PointsCost { get; private set; }

    public DateTime IssuedAt { get; private set; }

    // When the reservation lapses. Null on legacy `Issued` rows, which never held one.
    public DateTime? ReservationExpiresAt { get; private set; }

    public DateTime? RedeemedAt { get; private set; }

    public Guid? RedeemedByEmployeeId { get; private set; }

    public Guid? RedeemedBranchId { get; private set; }

    // Set on the reject path only — staff tell the customer why, and the audit trail keeps it.
    public string? RejectionReason { get; private set; }

    protected Coupon()
    {
        Code = string.Empty;
    }

    private Coupon(
        Guid id,
        Guid rewardId,
        Guid membershipId,
        string code,
        int pointsCost,
        DateTime issuedAt,
        DateTime reservationExpiresAt)
        : base(id)
    {
        RewardId = rewardId;
        MembershipId = membershipId;
        Code = code;
        PointsCost = pointsCost;
        IssuedAt = issuedAt;
        ReservationExpiresAt = reservationExpiresAt;
        Status = CouponStatus.Pending;
    }

    public static Coupon CreatePending(
        Guid id,
        Guid rewardId,
        Guid membershipId,
        string code,
        int pointsCost,
        DateTime issuedAt,
        DateTime reservationExpiresAt)
    {
        if (pointsCost <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pointsCost), "A redemption must cost points.");
        }

        return new Coupon(id, rewardId, membershipId, code, pointsCost, issuedAt, reservationExpiresAt);
    }

    public bool IsAwaitingApproval => Status == CouponStatus.Pending;

    // True only for rows whose points are still held rather than already debited. The one place this
    // matters is approval: a Pending coupon moves points, a legacy Issued one already did.
    public bool HoldsReservation => Status == CouponStatus.Pending;

    public bool HasLapsed(DateTime now) =>
        Status == CouponStatus.Pending && ReservationExpiresAt.HasValue && ReservationExpiresAt.Value <= now;

    public void Approve(DateTime redeemedAt, Guid redeemedByEmployeeId, Guid? redeemedBranchId)
    {
        // `Issued` is accepted alongside `Pending` for the legacy rows described on CouponStatus —
        // they are equally valid to hand over, they just have nothing left to debit.
        if (Status != CouponStatus.Pending && Status != CouponStatus.Issued)
        {
            throw new UserFriendlyException("This coupon has already been used or is no longer valid.");
        }

        Status = CouponStatus.Redeemed;
        RedeemedAt = redeemedAt;
        RedeemedByEmployeeId = redeemedByEmployeeId;
        RedeemedBranchId = redeemedBranchId;
    }

    public void Reject(Guid rejectedByEmployeeId, string? reason)
    {
        if (Status != CouponStatus.Pending)
        {
            throw new UserFriendlyException("This redemption is no longer awaiting approval.");
        }

        Status = CouponStatus.Cancelled;
        RedeemedByEmployeeId = rejectedByEmployeeId; // who acted on it — the audit trail wants this either way
        RejectionReason = reason;
    }

    public void MarkExpired()
    {
        if (Status == CouponStatus.Pending || Status == CouponStatus.Issued)
        {
            Status = CouponStatus.Expired;
        }
    }

    public void Cancel()
    {
        if (Status == CouponStatus.Pending || Status == CouponStatus.Issued)
        {
            Status = CouponStatus.Cancelled;
        }
    }
}
