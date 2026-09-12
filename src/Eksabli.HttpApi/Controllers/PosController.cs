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

    [HttpPost("preview-points/{customerId}")]
    public Task<PointsPreviewDto> PreviewPointsAsync(Guid customerId, PreviewPointsDto input)
    {
        return _posAppService.PreviewPointsAsync(customerId, input);
    }

    [HttpPost("adjust")]
    public Task<AwardPointsResultDto> ManualAdjustAsync(ManualAdjustDto input)
    {
        return _posAppService.ManualAdjustAsync(input);
    }

    // Read-only: staff scan or type a code and see who/what before deciding. POST rather than GET
    // because the code is a bearer-ish secret off a customer's screen — it does not belong in a URL,
    // a browser history or an access log.
    [HttpPost("lookup-redemption")]
    public Task<RedemptionLookupDto> LookupRedemptionAsync(LookupRedemptionDto input)
    {
        return _posAppService.LookupRedemptionAsync(input);
    }

    [HttpPost("confirm-redemption")]
    public Task<RedemptionConfirmationDto> ConfirmRedemptionAsync(ConfirmRedemptionDto input)
    {
        return _posAppService.ConfirmRedemptionAsync(input);
    }

    [HttpPost("reject-redemption")]
    public Task<RedemptionRejectionDto> RejectRedemptionAsync(RejectRedemptionDto input)
    {
        return _posAppService.RejectRedemptionAsync(input);
    }
}
