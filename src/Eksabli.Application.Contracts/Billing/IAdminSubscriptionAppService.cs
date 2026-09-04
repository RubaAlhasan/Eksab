using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Billing;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/AdminSubscriptionsController.cs).
[RemoteService(IsEnabled = false)]
public interface IAdminSubscriptionAppService : IApplicationService
{
    Task<PagedResultDto<TenantSubscriptionDto>> GetListAsync(AdminSubscriptionFilterDto input);

    Task<AdminSubscriptionStatsDto> GetStatsAsync();

    Task<List<MrrTrendPointDto>> GetMrrTrendAsync();

    Task<PagedResultDto<InvoiceDto>> GetInvoicesAsync(AdminInvoiceFilterDto input);

    Task<InvoiceDto> RecordManualPaymentAsync(RecordManualPaymentDto input);

    Task<PagedResultDto<PaymentDto>> GetPaymentsAsync(AdminPaymentFilterDto input);
}
