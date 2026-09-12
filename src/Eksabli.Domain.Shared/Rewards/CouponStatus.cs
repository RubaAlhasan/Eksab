namespace Eksabli.Rewards;

// Persisted as int — append new members, never renumber.
//
// The lifecycle is Pending -> (Redeemed | Cancelled | Expired). `Pending` is the state a coupon is
// born in under the reservation flow: the points are held on the wallet but NOT yet debited, and the
// coupon is worth nothing until a staff member approves it at the till.
//
// `Issued` is the LEGACY birth state, from before reservation existed: those coupons had their points
// debited at creation time. Rows in that state still exist in production data, so
// PosAppService.ConfirmRedemptionAsync deliberately accepts both — but only `Pending` moves points on
// approval. See that method's own comment.
public enum CouponStatus
{
    Issued = 0,
    Redeemed = 1,
    Expired = 2,
    Cancelled = 3,
    Pending = 4
}
