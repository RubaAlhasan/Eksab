using System.Threading.Tasks;
using Eksabli.SmartOffers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

// Staff counter. Role-gated inside SmartOfferOrderAppService, so the controller only requires an authenticated user.
[ApiController]
[Route("api/app/smart-offer-order")]
[Authorize]
public class SmartOfferOrdersController : EksabliController
{
    private readonly ISmartOfferOrderAppService _smartOfferOrderAppService;

    public SmartOfferOrdersController(ISmartOfferOrderAppService smartOfferOrderAppService)
    {
        _smartOfferOrderAppService = smartOfferOrderAppService;
    }

    [HttpPost("lookup")]
    public Task<SmartOfferOrderStaffDto> LookupAsync(SmartOfferOrderCodeDto input)
    {
        return _smartOfferOrderAppService.LookupAsync(input);
    }

    [HttpPost("complete")]
    public Task<SmartOfferOrderStaffDto> CompleteAsync(SmartOfferOrderCodeDto input)
    {
        return _smartOfferOrderAppService.CompleteAsync(input);
    }

    [HttpGet("history")]
    public Task<PagedResultDto<SmartOfferOrderStaffDto>> GetHistoryAsync([FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _smartOfferOrderAppService.GetHistoryAsync(input);
    }

    [HttpPost("reject")]
    public Task<SmartOfferOrderStaffDto> RejectAsync(RejectSmartOfferOrderDto input)
    {
        return _smartOfferOrderAppService.RejectAsync(input);
    }
}
