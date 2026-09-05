namespace Eksabli.Settings;

public static class EksabliSettings
{
    private const string Prefix = "Eksabli";

    public static class Trial
    {
        // Read by BusinessAppService.ProvisionTrialSubscriptionAsync — falls back to
        // Eksabli.Billing.BillingConsts.TrialDurationDays if unset (should never happen once
        // EksabliSettingDefinitionProvider's default value is seeded, but keeps the call site safe).
        public const string LengthDays = Prefix + ".Trial.LengthDays";
    }

    // Non-secret toggle only — real provider credentials belong in IConfiguration/appsettings.json
    // (matching Fcm:CredentialsFilePath), never in a Setting: Setting values are DB-stored and editable
    // via the Setting Management admin UI, which is not where secrets should live.
    public const string MaintenanceMode = Prefix + ".MaintenanceMode";

    public static class Sms
    {
        // Names which ISmsSender implementation is considered "active" for display/ops purposes
        // (e.g. "Null" today). The actual provider is still selected via DI registration in
        // EksabliDomainModule, not by reading this setting — this exists so Setting Management shows
        // which one is wired up without requiring a source read.
        public const string ActiveProvider = Prefix + ".Sms.ActiveProvider";
    }
}
