using System;
using System.Threading.Tasks;
using Eksabli.BusinessProfiles;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace Eksabli.Businesses;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/BusinessController.cs),
// not ABP's Auto API Controllers convention — excluded here to avoid a duplicate/conflicting route.
[RemoteService(IsEnabled = false)]
public interface IBusinessAppService : IApplicationService
{
    Task<BusinessRegistrationResultDto> RegisterAsync(RegisterBusinessDto input);

    Task<BusinessProfileDto> GetProfileAsync();

    Task<BusinessProfileDto> UpdateProfileAsync(UpdateBusinessProfileDto input);

    // PNG/JPEG/WebP only, capped at BusinessProfileConsts.MaxLogoFileSizeBytes — see
    // BusinessAppService.UploadLogoAsync for why the size cap is enforced on the stream itself rather
    // than trusting IRemoteStreamContent's caller-supplied ContentLength.
    Task<BusinessProfileDto> UploadLogoAsync(IRemoteStreamContent file);

    Task<BusinessProfileDto> RemoveLogoAsync();

    // Anonymous and keyed by id (not "the caller's own tenant") — the logo is meant to be publicly
    // viewable, e.g. from an <img> tag with no auth context, same as any other public-facing image URL.
    Task<IRemoteStreamContent> GetLogoAsync(Guid businessProfileId);
}
