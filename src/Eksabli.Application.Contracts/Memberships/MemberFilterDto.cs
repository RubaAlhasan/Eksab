using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Memberships;

public class MemberFilterDto : PagedAndSortedResultRequestDto
{
    // Matches against name (first/last) or phone number.
    public string? FilterText { get; set; }

    public Guid? TierId { get; set; }

    public MembershipStatus? Status { get; set; }

    // Restricts the list to members with at least one real points-earning ("charge") transaction —
    // PointsWallet.LifetimeEarned > 0, which only a real Type=Earn transaction ever increments (see
    // PointsWallet.ApplyTransaction). Used by the Customers > Members tab specifically, so a business
    // doesn't see everyone who's ever joined/scanned a QR, including people who never actually came in
    // and bought anything. A membership that only ever got a manual goodwill adjustment (no real
    // purchase) is deliberately still excluded — that's not "coming to the store" either.
    //
    // Every OTHER caller of GetMembersAsync (Coupons' name lookup, Notifications' recipient picker,
    // the Subscription page's Active-Members usage count) leaves this null/false on purpose, to keep
    // their own existing, correct behavior unchanged — see MembershipAppService.GetMembersAsync's own
    // comment.
    public bool? HasEarnedPointsAtLeastOnce { get; set; }
}
