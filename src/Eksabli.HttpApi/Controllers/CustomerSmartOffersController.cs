using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.SmartOffers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/customer-smart-offer")]
[Authorize]
public class CustomerSmartOffersController : EksabliController
{
    private readonly ICustomerSmartOfferAppService _customerSmartOfferAppService;

    public CustomerSmartOffersController(ICustomerSmartOfferAppService customerSmartOfferAppService)
    {
        _customerSmartOfferAppService = customerSmartOfferAppService;
    }

    [HttpGet("tenant/{tenantId}")]
    public Task<CustomerSmartOfferListDto> GetOffersAsync(Guid tenantId)
    {
        return _customerSmartOfferAppService.GetOffersAsync(tenantId);
    }

    [HttpGet("feed")]
    public Task<CustomerSmartOfferListDto> GetFeedAsync([FromQuery] int maxResultCount = 30)
    {
        return _customerSmartOfferAppService.GetFeedAsync(maxResultCount);
    }

    [HttpGet("orders/mine")]
    public Task<PagedResultDto<CustomerSmartOfferOrderDto>> GetMyOrdersAsync([FromQuery] GetMySmartOfferOrdersInput input)
    {
        return _customerSmartOfferAppService.GetMyOrdersAsync(input);
    }

    [HttpPost("orders")]
    public Task<SmartOfferOrderDto> PlaceOrderAsync(PlaceSmartOfferOrderDto input)
    {
        return _customerSmartOfferAppService.PlaceOrderAsync(input);
    }

    // Opened from an order's "View deal" link. Works after the deal's sale window has ended.
    [HttpGet("tenant/{tenantId}/offers/{offerId}")]
    public Task<CustomerSmartOfferDetailsDto> GetOfferDetailsAsync(Guid tenantId, Guid offerId)
    {
        return _customerSmartOfferAppService.GetOfferDetailsAsync(tenantId, offerId);
    }

    // "Tell me when this price drops." PUT and DELETE rather than POST so repeating either call is harmless.
    [HttpPut("tenant/{tenantId}/offers/{offerId}/price-watch")]
    public Task WatchPriceAsync(Guid tenantId, Guid offerId)
    {
        return _customerSmartOfferAppService.WatchPriceAsync(tenantId, offerId);
    }

    [HttpDelete("tenant/{tenantId}/offers/{offerId}/price-watch")]
    public Task UnwatchPriceAsync(Guid tenantId, Guid offerId)
    {
        return _customerSmartOfferAppService.UnwatchPriceAsync(tenantId, offerId);
    }

    [HttpGet("price-watches/mine")]
    public Task<List<SmartOfferWatchDto>> GetMyPriceWatchesAsync()
    {
        return _customerSmartOfferAppService.GetMyPriceWatchesAsync();
    }

    [HttpGet("orders/{tenantId}/{orderId}")]
    public Task<SmartOfferOrderDto> GetMyOrderAsync(Guid tenantId, Guid orderId)
    {
        return _customerSmartOfferAppService.GetMyOrderAsync(tenantId, orderId);
    }

    [HttpPost("orders/{tenantId}/{orderId}/cancel")]
    public Task<SmartOfferOrderDto> CancelMyOrderAsync(Guid tenantId, Guid orderId)
    {
        return _customerSmartOfferAppService.CancelMyOrderAsync(tenantId, orderId);
    }
}
