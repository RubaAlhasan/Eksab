namespace Eksabli.BusinessProfiles;

// Manual approval queue until self-serve moderation tooling exists — see
// docs/eksabli-loyalty-platform/features/08-admin-panel/README.md#business-rules. Not currently wired
// into customer-facing discovery/search (that feature doesn't exist yet); Suspended is enforced at the
// one real touchpoint that does exist today — MembershipAppService.JoinAsync blocks new joins.
public enum TenantApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Suspended = 2
}
