namespace Eksabli.BusinessProfiles;

public static class BusinessProfileConsts
{
    public const int MaxDescriptionLength = 2000;
    public const int MaxWebsiteLength = 256;
    public const int MaxLogoBlobNameLength = 256;
    public const int MaxLogoContentTypeLength = 100;
    public const int MaxSocialLinksJsonLength = 2000;

    public const int MaxLogoFileSizeBytes = 2 * 1024 * 1024; // 2 MB

    public static readonly string[] AllowedLogoContentTypes = { "image/png", "image/jpeg", "image/webp" };
}
