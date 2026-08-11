namespace Eksabli.BusinessProfiles;

// Manual approval queue until self-serve moderation tooling exists — see
// docs/eksabli-loyalty-platform/features/08-admin-panel/README.md#business-rules. Not currently wired
// into customer-facing discovery/search (that feature doesn't exist yet); enforced at two real
// touchpoints — MembershipAppService.JoinAsync blocks new joins for anything other than Approved
// (Pending or Suspended), and the Business Portal's own businessApprovalGuard (Angular) blocks a
// Pending/Suspended business's own staff from the dashboard.
public enum TenantApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Suspended = 2
}
