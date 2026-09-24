namespace Eksabli.Memberships;

public enum MembershipStatus
{
    Active,
    Frozen,

    // Customer-initiated "leave this business" — deliberately distinct from Frozen (staff-initiated
    // suspension, e.g. abuse) so a business can't confuse the two reasons a member went inactive.
    // Cancelling never deletes the Membership/PointsWallet — balance and history survive — so
    // MembershipAppService.JoinAsync can reactivate a Cancelled membership in place if the customer
    // comes back, rather than starting them over from zero.
    Cancelled
}
