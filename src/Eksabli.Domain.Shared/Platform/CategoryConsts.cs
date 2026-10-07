namespace Eksabli.Platform;

public static class CategoryConsts
{
    public const int MaxNameLength = 128;
    public const int MaxIconBlobNameLength = 256;
    public const int MaxIconContentTypeLength = 100;

    // A category icon is a small glyph, not a photo — a lower cap than a business logo's keeps the shared
    // taxonomy's storage bounded regardless of how many categories accumulate icons over time.
    public const int MaxIconFileSizeBytes = 512 * 1024; // 512 KB

    public static readonly string[] AllowedIconContentTypes = { "image/png", "image/jpeg", "image/webp" };
}
