using System.Threading.Tasks;
using Eksabli.Otp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/otp")]
[AllowAnonymous]
public class OtpController : EksabliController
{
    private readonly IOtpAppService _otpAppService;

    public OtpController(IOtpAppService otpAppService)
    {
        _otpAppService = otpAppService;
    }

    [HttpPost("request")]
    public Task RequestOtpAsync(RequestOtpDto input)
    {
        return _otpAppService.RequestOtpAsync(input);
    }

    // Call before completing sign-in via POST /connect/token (grant_type=otp) so a first-time
    // customer's CustomerProfile comes out already named. See IOtpAppService.RegisterAsync.
    [HttpPost("register")]
    public Task RegisterAsync(RegisterCustomerDto input)
    {
        return _otpAppService.RegisterAsync(input);
    }
}
