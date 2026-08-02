namespace Eksabli.Notifications;

public static class NotificationConsts
{
    public const int MaxTitleLength = 128;
    public const int MaxBodyLength = 1000;

    // Per-tenant fan-out cap for the CampaignSweepWorker — a named risk in
    // docs/eksabli-loyalty-platform/01-business-strategy.md#21-risks: one business's campaign must not
    // be able to starve another's transactional notifications. Applies per calendar day per tenant.
    public const int MaxDailyNotificationsPerTenant = 500;
}
