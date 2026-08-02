using System;
using System.Threading.Tasks;
using Eksabli.Billing;
using Eksabli.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/subscription-plan")]
public class SubscriptionPlansController : EksabliController
{
    private readonly ISubscriptionPlanAppService _subscriptionPlanAppService;

    public SubscriptionPlansController(ISubscriptionPlanAppService subscriptionPlanAppService)
    {
        _subscriptionPlanAppService = subscriptionPlanAppService;
    }

    // Public pricing catalog — a prospective business needs to see plans before registering.
    [AllowAnonymous]
    [HttpGet("{id}")]
    public Task<SubscriptionPlanDto> GetAsync(Guid id)
    {
        return _subscriptionPlanAppService.GetAsync(id);
    }

    [AllowAnonymous]
    [HttpGet]
    public Task<PagedResultDto<SubscriptionPlanDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _subscriptionPlanAppService.GetListAsync(input);
    }

    [Authorize(EksabliPermissions.Billing.ManagePlatform)]
    [HttpPost]
    public Task<SubscriptionPlanDto> CreateAsync(CreateUpdateSubscriptionPlanDto input)
    {
        return _subscriptionPlanAppService.CreateAsync(input);
    }

    [Authorize(EksabliPermissions.Billing.ManagePlatform)]
    [HttpPut("{id}")]
    public Task<SubscriptionPlanDto> UpdateAsync(Guid id, CreateUpdateSubscriptionPlanDto input)
    {
        return _subscriptionPlanAppService.UpdateAsync(id, input);
    }

    [Authorize(EksabliPermissions.Billing.ManagePlatform)]
    [HttpDelete("{id}")]
    public Task DeleteAsync(Guid id)
    {
        return _subscriptionPlanAppService.DeleteAsync(id);
    }
}
