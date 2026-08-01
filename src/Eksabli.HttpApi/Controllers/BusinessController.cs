using System.Threading.Tasks;
using Eksabli.Businesses;
using Eksabli.BusinessProfiles;
using Eksabli.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
}
