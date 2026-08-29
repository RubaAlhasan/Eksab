using System;
using System.Threading.Tasks;
using Eksabli.Businesses;
using Eksabli.BusinessProfiles;
using Eksabli.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Content;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/business")]
public class BusinessController : EksabliController
{
    private readonly IBusinessAppService _businessAppService;

    public BusinessController(IBusinessAppService businessAppService)
    {
        _businessAppService = businessAppService;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public Task<BusinessRegistrationResultDto> RegisterAsync(RegisterBusinessDto input)
    {
        return _businessAppService.RegisterAsync(input);
    }

    [Authorize(EksabliPermissions.BusinessProfile.Default)]
    [HttpGet("profile")]
    public Task<BusinessProfileDto> GetProfileAsync()
    {
        return _businessAppService.GetProfileAsync();
    }

    [Authorize(EksabliPermissions.BusinessProfile.Edit)]
    [HttpPut("profile")]
    public Task<BusinessProfileDto> UpdateProfileAsync(UpdateBusinessProfileDto input)
    {
        return _businessAppService.UpdateProfileAsync(input);
    }

    [Authorize(EksabliPermissions.BusinessProfile.Edit)]
    [HttpPut("profile/logo")]
    public Task<BusinessProfileDto> UploadLogoAsync(IRemoteStreamContent file)
    {
        return _businessAppService.UploadLogoAsync(file);
    }

    [Authorize(EksabliPermissions.BusinessProfile.Edit)]
    [HttpDelete("profile/logo")]
    public Task<BusinessProfileDto> RemoveLogoAsync()
    {
        return _businessAppService.RemoveLogoAsync();
    }

    // Public — no [Authorize] — so it works as a plain <img src> URL with no auth context. Keyed by id,
    // not "the caller's own tenant", since anonymous callers have no tenant to resolve from.
    [AllowAnonymous]
    [HttpGet("{businessProfileId}/logo")]
    public Task<IRemoteStreamContent> GetLogoAsync(Guid businessProfileId)
    {
        return _businessAppService.GetLogoAsync(businessProfileId);
    }
}
