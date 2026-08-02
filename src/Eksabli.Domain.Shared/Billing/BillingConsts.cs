namespace Eksabli.Billing;

public static class BillingConsts
{
    public const int TrialDurationDays = 14;
}

public static class SubscriptionPlanConsts
{
    public const int MaxNameLength = 64;
    public const int MaxFeatureLimitsJsonLength = 2000;
}

public static class PaymentConsts
{
    public const int MaxProviderLength = 32;
    public const int MaxProviderTransactionRefLength = 128;
}
