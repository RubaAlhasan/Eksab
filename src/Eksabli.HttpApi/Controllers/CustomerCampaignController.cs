using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Campaigns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eksabli.Controllers;

// Customer-facing campaign feed, used by the mobile app's Campaigns screen and
// the Store Details offers tab.
//
// Separate from CampaignsController, which is the Business Portal's management
// surface: that one requires Campaigns.Default and exposes targeting rules.
[ApiController]
[Route("api/app/customer-campaign")]
[Authorize]
public class CustomerCampaignController : EksabliController
{
    private readonly ICustomerCampaignAppService _customerCampaignAppService;

    public CustomerCampaignController(ICustomerCampaignAppService customerCampaignAppService)
    {
        _customerCampaignAppService = customerCampaignAppService;
    }

    // Live campaigns across every business the customer has joined.
    [HttpGet("my")]
    public Task<List<CustomerCampaignDto>> GetMyFeedAsync()
    {
        return _customerCampaignAppService.GetMyFeedAsync();
    }

    [HttpGet("business/{tenantId}")]
    public Task<List<CustomerCampaignDto>> GetForBusinessAsync(Guid tenantId)
    {
        return _customerCampaignAppService.GetForBusinessAsync(tenantId);
    }
}
