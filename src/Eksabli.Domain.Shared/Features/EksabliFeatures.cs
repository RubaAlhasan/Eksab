namespace Eksabli.Features;

public static class EksabliFeatures
{
    public const string GroupName = "Eksabli";

    public const string MaxBranches = GroupName + ".MaxBranches";
    public const string MaxActiveMembers = GroupName + ".MaxActiveMembers";
    public const string MaxCampaigns = GroupName + ".MaxCampaigns";
    public const string SmsNotifications = GroupName + ".SMSNotifications";
    public const string PushNotifications = GroupName + ".PushNotifications";

    // Tenant-level opt-in for badges/achievements — not every business category wants gamification
    // UI, see docs/eksabli-loyalty-platform/features/06-engagement-gamification/README.md.
    public const string Gamification = GroupName + ".Gamification";
}
