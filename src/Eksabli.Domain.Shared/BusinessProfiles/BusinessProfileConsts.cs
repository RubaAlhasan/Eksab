namespace Eksabli.BusinessProfiles;

public static class BusinessProfileConsts
{
    public const int MaxDisplayNameLength = 128;
    public const int MaxDescriptionLength = 2000;
    public const int MaxWebsiteLength = 256;
    public const int MaxLogoBlobNameLength = 256;
    public const int MaxLogoContentTypeLength = 100;
    public const int MaxSocialLinksJsonLength = 2000;
    public const int MaxTimeZoneIdLength = 64;

    // The zone a business runs on when none has been chosen. Drives "today", day boundaries and peak hours on
    // the Business dashboard. Editable from Business Settings.
    public const string DefaultTimeZoneId = "Asia/Damascus";

    // How long points a business awards stay spendable, in whole months from the moment they are earned.
    // Null on the profile means points never expire. Set from Business Settings.
    public const int MinPointsExpiryMonths = 1;
    public const int MaxPointsExpiryMonths = 120;

    public const int MaxLogoFileSizeBytes = 2 * 1024 * 1024; // 2 MB

    public static readonly string[] AllowedLogoContentTypes = { "image/png", "image/jpeg", "image/webp" };
}
