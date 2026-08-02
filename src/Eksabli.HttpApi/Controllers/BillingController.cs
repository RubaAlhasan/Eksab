using System.Threading.Tasks;
using Eksabli.Billing;
using Eksabli.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/billing")]
[Authorize(EksabliPermissions.Billing.ManageOwn)]
public class BillingController : EksabliController
{
    private readonly IBillingAppService _billingAppService;

    public BillingController(IBillingAppService billingAppService)
    {
        _billingAppService = billingAppService;
    }

    [HttpGet("my-subscription")]
    public Task<TenantSubscriptionDto> GetMyCurrentSubscriptionAsync()
    {
        return _billingAppService.GetMyCurrentSubscriptionAsync();
    }

    [HttpGet("my-usage")]
    public Task<UsageDto> GetMyUsageAsync()
    {
        return _billingAppService.GetMyUsageAsync();
    }

    [HttpGet("my-invoices")]
    public Task<PagedResultDto<InvoiceDto>> GetMyInvoicesAsync([FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _billingAppService.GetMyInvoicesAsync(input);
    }

    [HttpPost("change-plan")]
    public Task<TenantSubscriptionDto> ChangePlanAsync(ChangePlanDto input)
    {
        return _billingAppService.ChangePlanAsync(input);
    }
}
