using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace Eksabli.Campaigns;

// Customer-facing campaign feed. The business-portal ICampaignAppService is
// gated behind Campaigns.Default, which no customer holds, and exposes
// targeting internals — hence a separate, read-only, customer-safe service.
//
// Exposed via src/Eksabli.HttpApi/Controllers/CustomerCampaignController.cs.
[RemoteService(IsEnabled = false)]
public interface ICustomerCampaignAppService : IApplicationService
{
    // Live campaigns across every business the customer has joined, filtered to
    // those whose target segment actually includes them.
    Task<List<CustomerCampaignDto>> GetMyFeedAsync();

    // Live campaigns for one business — the Store Details "Offers" tab.
    Task<List<CustomerCampaignDto>> GetForBusinessAsync(Guid tenantId);
}
