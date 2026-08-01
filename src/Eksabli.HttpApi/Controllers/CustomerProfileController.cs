using System.Threading.Tasks;
using Eksabli.CustomerProfiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/customer-profile")]
[Authorize]
public class CustomerProfileController : EksabliController
{
    private readonly ICustomerProfileAppService _customerProfileAppService;

    public CustomerProfileController(ICustomerProfileAppService customerProfileAppService)
    {
        _customerProfileAppService = customerProfileAppService;
    }

    [HttpGet("my")]
    public Task<CustomerProfileDto> GetMyProfileAsync()
    {
        return _customerProfileAppService.GetMyProfileAsync();
    }

    [HttpPut("my")]
    public Task<CustomerProfileDto> UpdateMyProfileAsync(UpdateCustomerProfileDto input)
    {
        return _customerProfileAppService.UpdateMyProfileAsync(input);
    }
}
