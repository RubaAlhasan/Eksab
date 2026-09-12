using System;
using Eksabli.Memberships;

namespace Eksabli.Platform;

// One row of the Admin Portal > Users > Customer Details "Businesses & Wallets" table — this
// customer's independent balance at ONE business, per the two-realm identity design
// (docs/eksabli-loyalty-platform/02-system-architecture.md#two-identity-realms-the-key-decision). A
// customer has one of these per business membership; Balance/LifetimeEarned/LifetimeRedeemed are never
// summed across rows in the UI — each business's points are its own currency, not a shared total (same
// reasoning as the "redemption rate must stay per-business" note in
// docs/eksabli-loyalty-platform/06-dashboards-admin.md#12-reports--analytics).
public class AdminCustomerMembershipDto
{
    public Guid MembershipId { get; set; }

    public Guid TenantId { get; set; }

    public string BusinessName { get; set; } = string.Empty;

    public MembershipStatus Status { get; set; }

    public DateTime JoinedAt { get; set; }

    public int Balance { get; set; }

    public int LifetimeEarned { get; set; }

    public int LifetimeRedeemed { get; set; }

    // Null when the wallet has no current tier (no Tier configured yet, or hasn't qualified for one).
    public string? TierName { get; set; }
}
