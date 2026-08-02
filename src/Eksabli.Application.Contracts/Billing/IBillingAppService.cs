using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Billing;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/BillingController.cs).
[RemoteService(IsEnabled = false)]
public interface IBillingAppService : IApplicationService
{
    Task<TenantSubscriptionDto> GetMyCurrentSubscriptionAsync();

    Task<UsageDto> GetMyUsageAsync();

    Task<PagedResultDto<InvoiceDto>> GetMyInvoicesAsync(PagedAndSortedResultRequestDto input);

    Task<TenantSubscriptionDto> ChangePlanAsync(ChangePlanDto input);
}
