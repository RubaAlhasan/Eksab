namespace Eksabli.Engagement;

public static class ReferralConsts
{
    // Flat bonus paid to both referrer and referee on completion — see
    // docs/eksabli-loyalty-platform/07-loyalty-engine.md#11-customer-engagement. Not per-tenant
    // configurable yet; revisit if/when a tenant asks for a different amount.
    public const int BonusPoints = 100;
}
