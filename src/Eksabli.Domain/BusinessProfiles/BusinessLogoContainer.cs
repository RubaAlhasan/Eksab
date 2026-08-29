using Volo.Abp.BlobStoring;

namespace Eksabli.BusinessProfiles;

// Marker type selecting the "business-logos" AbpBlobStoringOptions container config (registered in
// EksabliDomainModule.ConfigureBlobStoring) — resolved via IBlobContainer<BusinessLogoContainer>, the
// same generic-type-selector convention ABP itself uses for typed blob containers. No members of its own.
[BlobContainerName("business-logos")]
public class BusinessLogoContainer
{
}
