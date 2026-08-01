namespace Eksabli.Wallets;

public static class PointsTransactionConsts
{
    // Fraud/error blast-radius cap on manual adjustments — MVP hardcoded default.
    // Candidate for a real tenant-level SettingManagement value later.
    public const int MaxDailyManualAdjustmentsPerEmployee = 20;

    public const int MaxReasonLength = 256;
}
