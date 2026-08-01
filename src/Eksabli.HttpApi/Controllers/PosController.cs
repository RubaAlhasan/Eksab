using System;
using System.Threading.Tasks;
using Eksabli.Pos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/pos")]
[Authorize]
public class PosController : EksabliController
{
    private readonly IPosAppService _posAppService;

    public PosController(IPosAppService posAppService)
    {
        _posAppService = posAppService;
    }

    [HttpPost("lookup-by-phone")]
    public Task<CustomerLookupResultDto> LookupCustomerByPhoneAsync(PhoneLookupDto input)
    {
        return _posAppService.LookupCustomerByPhoneAsync(input);
    }

    [HttpPost("award-points/qr")]
    public Task<AwardPointsResultDto> AwardPointsByQrAsync(AwardPointsByQrDto input)
    {
        return _posAppService.AwardPointsByQrAsync(input);
    }

    [HttpPost("award-points/customer/{customerId}")]
    public Task<AwardPointsResultDto> AwardPointsByCustomerIdAsync(Guid customerId, AwardPointsByCustomerIdDto input)
    {
        return _posAppService.AwardPointsByCustomerIdAsync(customerId, input);
    }

    [HttpPost("adjust")]
    public Task<AwardPointsResultDto> ManualAdjustAsync(ManualAdjustDto input)
    {
        return _posAppService.ManualAdjustAsync(input);
    }
}
