using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Billing;
using Eksabli.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/admin-subscriptions")]
[Authorize(EksabliPermissions.Billing.ManagePlatform)]
public class AdminSubscriptionsController : EksabliController
{
    private readonly IAdminSubscriptionAppService _adminSubscriptionAppService;

    public AdminSubscriptionsController(IAdminSubscriptionAppService adminSubscriptionAppService)
    {
        _adminSubscriptionAppService = adminSubscriptionAppService;
    }

    [HttpGet]
    public Task<PagedResultDto<TenantSubscriptionDto>> GetListAsync([FromQuery] AdminSubscriptionFilterDto input)
    {
        return _adminSubscriptionAppService.GetListAsync(input);
    }

    [HttpGet("stats")]
    public Task<AdminSubscriptionStatsDto> GetStatsAsync()
    {
        return _adminSubscriptionAppService.GetStatsAsync();
    }

    [HttpGet("mrr-trend")]
    public Task<List<MrrTrendPointDto>> GetMrrTrendAsync()
    {
        return _adminSubscriptionAppService.GetMrrTrendAsync();
    }

    [HttpGet("invoices")]
    public Task<PagedResultDto<InvoiceDto>> GetInvoicesAsync([FromQuery] AdminInvoiceFilterDto input)
    {
        return _adminSubscriptionAppService.GetInvoicesAsync(input);
    }

    [HttpPost("record-manual-payment")]
    public Task<InvoiceDto> RecordManualPaymentAsync(RecordManualPaymentDto input)
    {
        return _adminSubscriptionAppService.RecordManualPaymentAsync(input);
    }
}
