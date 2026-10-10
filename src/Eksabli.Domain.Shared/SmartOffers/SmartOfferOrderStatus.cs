namespace Eksabli.SmartOffers;

// Persisted as int — append new members, never renumber.
//
// Lifecycle (mirrors the coupon reservation flow):
//
//     customer taps Buy Now           staff complete at the counter -> Completed (stock becomes Sold)
//     -> Pending (stock held) ------- staff reject                  -> Rejected  (stock released)
//                                 \-- customer cancels              -> Cancelled (stock released)
//                                  \- window closes, worker         -> Expired   (stock released)
public enum SmartOfferOrderStatus
{
    Pending = 0,
    Completed = 1,
    Cancelled = 2,
    Rejected = 3,
    Expired = 4
}
