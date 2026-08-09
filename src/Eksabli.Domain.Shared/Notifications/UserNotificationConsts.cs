namespace Eksabli.Notifications;

public static class UserNotificationConsts
{
    public const int MaxTitleLength = 128;
    public const int MaxMessageLength = 1000;

    // Category is a short machine-readable event key (e.g. "subscription.past_due",
    // "audit.suspicious_login") the Angular/Flutter clients can switch on for deep-linking — free-form
    // on purpose so new application events never need a schema change here.
    public const int MaxCategoryLength = 64;

    // Serialized JSON payload handed to the client alongside Title/Message — e.g. { "entityId": "..." }
    // so a click can route straight to the relevant screen. Kept generous but bounded.
    public const int MaxDataLength = 2000;
}
