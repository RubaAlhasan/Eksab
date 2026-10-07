using Volo.Abp.BlobStoring;

namespace Eksabli.Platform;

// Marker type selecting the "category-icons" AbpBlobStoringOptions container config (registered in
// EksabliDomainModule.ConfigureBlobStoring) — resolved via IBlobContainer<CategoryIconContainer>, the same
// generic-type-selector convention BusinessLogoContainer already uses. No members of its own.
[BlobContainerName("category-icons")]
public class CategoryIconContainer
{
}
