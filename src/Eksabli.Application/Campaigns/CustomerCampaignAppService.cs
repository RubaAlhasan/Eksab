using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.BusinessProfiles;
using Eksabli.Memberships;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace Eksabli.Campaigns;

// Customer-facing campaign feed.
//
// Two things make this different from the business-portal ICampaignAppService,
// and both are the reason it exists separately rather than relaxing that one's
// permissions:
//
//  1. It never returns targeting internals (RulesJson, TargetRules) — how a
//     business segments its members is not customer-visible.
//  2. It only returns campaigns whose target segment actually contains this
//     customer. Showing a VIP-only or win-back campaign to everyone would be
//     advertising something they cannot redeem, so the same
//     ICampaignSegmentEvaluator the Business Portal previews with is reused
//     here to decide.
[Authorize]
[RemoteService(IsEnabled = false)]
public class CustomerCampaignAppService : ApplicationService, ICustomerCampaignAppService
{
    private readonly IRepository<Campaign, Guid> _campaignRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<BusinessProfile, Guid> _businessProfileRepository;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly ICampaignSegmentEvaluator _segmentEvaluator;
    private readonly ICurrentUser _currentUser;
    private readonly IDataFilter _dataFilter;
    private readonly IClock _clock;

    public CustomerCampaignAppService(
        IRepository<Campaign, Guid> campaignRepository,
        IRepository<Membership, Guid> membershipRepository,
        IRepository<BusinessProfile, Guid> businessProfileRepository,
        IRepository<Tenant, Guid> tenantRepository,
        ICampaignSegmentEvaluator segmentEvaluator,
        ICurrentUser currentUser,
        IDataFilter dataFilter,
        IClock clock)
    {
        _campaignRepository = campaignRepository;
        _membershipRepository = membershipRepository;
        _businessProfileRepository = businessProfileRepository;
        _tenantRepository = tenantRepository;
        _segmentEvaluator = segmentEvaluator;
        _currentUser = currentUser;
        _dataFilter = dataFilter;
        _clock = clock;
    }

    public Task<List<CustomerCampaignDto>> GetMyFeedAsync() => BuildAsync(null);

    public Task<List<CustomerCampaignDto>> GetForBusinessAsync(Guid tenantId) =>
        BuildAsync(tenantId);

    // Shared path: the feed and the single-business tab differ only by scope.
    private async Task<List<CustomerCampaignDto>> BuildAsync(Guid? onlyTenantId)
    {
        var customerId = _currentUser.GetId();
        var now = _clock.Now;

        // Customers are Host-realm; campaigns, memberships and profiles are all
        // tenant-scoped, so every query here would return empty without this.
        using (_dataFilter.Disable<IMultiTenant>())
        {
            // A campaign is only relevant for a business the customer belongs
            // to — a promotion at a business you have not joined isn't yours.
            var memberships = await _membershipRepository.GetListAsync(m =>
                m.CustomerId == customerId &&
                (onlyTenantId == null || m.TenantId == onlyTenantId));

            var membershipIds = memberships.Select(m => m.Id).ToHashSet();
            var tenantIds = memberships
                .Where(m => m.TenantId.HasValue)
                .Select(m => m.TenantId!.Value)
                .Distinct()
                .ToList();

            if (tenantIds.Count == 0)
            {
                return new List<CustomerCampaignDto>();
            }

            // Suspended or Pending businesses should not be promoting anything.
            var approvedTenantIds = (await _businessProfileRepository.GetListAsync(p =>
                    p.TenantId != null && tenantIds.Contains(p.TenantId.Value)))
                .Where(p => p.ApprovalStatus == TenantApprovalStatus.Approved)
                .Select(p => p.TenantId!.Value)
                .ToHashSet();

            if (approvedTenantIds.Count == 0)
            {
                return new List<CustomerCampaignDto>();
            }

            // Active *and* inside its own window — a campaign left in Active
            // past its end date is over as far as a customer is concerned.
            var campaigns = (await _campaignRepository.GetListAsync(c =>
                    c.TenantId != null &&
                    c.Status == CampaignStatus.Active))
                .Where(c =>
                    approvedTenantIds.Contains(c.TenantId!.Value) &&
                    c.StartDate <= now &&
                    c.EndDate >= now)
                .ToList();

            if (campaigns.Count == 0)
            {
                return new List<CustomerCampaignDto>();
            }

            var tenantNames = (await _tenantRepository.GetListAsync(t =>
                    approvedTenantIds.Contains(t.Id)))
                .ToDictionary(t => t.Id, t => t.Name);

            var results = new List<CustomerCampaignDto>();
            foreach (var campaign in campaigns)
            {
                if (!await TargetsCustomerAsync(campaign, membershipIds))
                {
                    continue;
                }

                results.Add(new CustomerCampaignDto
                {
                    Id = campaign.Id,
                    TenantId = campaign.TenantId!.Value,
                    BusinessName = tenantNames.GetValueOrDefault(campaign.TenantId!.Value) ?? string.Empty,
                    NameAr = campaign.NameAr,
                    NameEn = campaign.NameEn,
                    Type = campaign.Type,
                    StartDate = campaign.StartDate,
                    EndDate = campaign.EndDate,
                });
            }

            return results.OrderBy(c => c.EndDate).ToList();
        }
    }

    // Reuses the Business Portal's own evaluator so "who does this campaign
    // apply to" has exactly one implementation. A campaign with no target rules
    // is untargeted and therefore applies to every member.
    private async Task<bool> TargetsCustomerAsync(Campaign campaign, IReadOnlySet<Guid> membershipIds)
    {
        if (campaign.TargetRules.Count == 0)
        {
            return true;
        }

        var matched = await _segmentEvaluator.EvaluateAsync(campaign);
        return matched.Any(m => membershipIds.Contains(m.Id));
    }
}
