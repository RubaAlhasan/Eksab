namespace Eksabli.Engagement;

public static class ReferralConsts
{
    // Flat bonus paid to both referrer and referee on completion — see
    // docs/eksabli-loyalty-platform/07-loyalty-engine.md#11-customer-engagement. Not per-tenant
    // configurable yet; revisit if/when a tenant asks for a different amount.
    public const int BonusPoints = 100;

    // Same length/shape as Rewards.CouponConsts.CodeLength — an 8-char uppercase hex code (32 bits of
    // entropy from a fresh GUID), the established pattern in this codebase for "short, human-typeable
    // code derived from a GUID" (see Rewards.CouponAppService.GenerateUniqueCodeAsync). Unique per
    // tenant, not globally — see Membership.ReferralCode's own comment for why that's enough here.
    public const int CodeLength = 8;
}
