using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.SmartOffers;

// Customer-side surface. Exposed via CustomerSmartOffersController. Requires an authenticated customer; ordering
// also requires an active membership at the business.
[RemoteService(IsEnabled = false)]
public interface ICustomerSmartOfferAppService : IApplicationService
{
    Task<CustomerSmartOfferListDto> GetOffersAsync(Guid tenantId);

    Task<SmartOfferOrderDto> PlaceOrderAsync(PlaceSmartOfferOrderDto input);

    Task<SmartOfferOrderDto> GetMyOrderAsync(Guid tenantId, Guid orderId);

    Task<SmartOfferOrderDto> CancelMyOrderAsync(Guid tenantId, Guid orderId);

    // Live deals across every business the customer has joined or follows (approved businesses only), the same selection
    // the campaign feed uses. Feeds the home page and the browse page.
    Task<CustomerSmartOfferListDto> GetFeedAsync(int maxResultCount = 30);

    // The caller's orders across every business, newest first, optionally filtered on the server.
    Task<PagedResultDto<CustomerSmartOfferOrderDto>> GetMyOrdersAsync(GetMySmartOfferOrdersInput input);

    // One deal, opened from the customer's own order. Allowed only when the caller has ordered that deal at this
    // business, and not limited to the sale window, so the details stay readable after it has ended.
    Task<CustomerSmartOfferDetailsDto> GetOfferDetailsAsync(Guid tenantId, Guid offerId);

    // "Tell me when this price drops." Idempotent. Refused for a deal at a business the customer does not belong to.
    Task WatchPriceAsync(Guid tenantId, Guid offerId);

    Task UnwatchPriceAsync(Guid tenantId, Guid offerId);

    // The customer's watches across every business, for the app to show which deals are already being watched.
    Task<List<SmartOfferWatchDto>> GetMyPriceWatchesAsync();
}
