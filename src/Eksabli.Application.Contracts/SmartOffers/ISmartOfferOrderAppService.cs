using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.SmartOffers;

// Staff counter surface for Buy Now orders. Exposed via SmartOfferOrdersController. Role-gated the same way POS
// redemptions are (see SmartOfferOrderAppService), not via ABP permissions, so a Cashier can use it.
[RemoteService(IsEnabled = false)]
public interface ISmartOfferOrderAppService : IApplicationService
{
    Task<SmartOfferOrderStaffDto> LookupAsync(SmartOfferOrderCodeDto input);

    Task<SmartOfferOrderStaffDto> CompleteAsync(SmartOfferOrderCodeDto input);

    Task<SmartOfferOrderStaffDto> RejectAsync(RejectSmartOfferOrderDto input);

    // Settled orders (completed, rejected, cancelled or expired), newest first. Lets the counter show what it sold after a refresh.
    Task<PagedResultDto<SmartOfferOrderStaffDto>> GetHistoryAsync(PagedAndSortedResultRequestDto input);
}
