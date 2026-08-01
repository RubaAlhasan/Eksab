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
}
