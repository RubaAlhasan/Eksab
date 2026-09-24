using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.BusinessProfiles;
using Eksabli.Engagement;
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
//
// A customer who FOLLOWS a business without joining also sees that business's broadly-targeted
// campaigns (untargeted, or an explicit CampaignTargetRuleSegmentType.All rule) — see Follow's own
// comment on deliberately doubling as a marketing-target concept, which was never wired up anywhere
// until this. Tier/New Customer/Inactive-restricted campaigns stay member-only: those need real
// Membership data (join date, wallet, tier) a follower doesn't have, and ICampaignSegmentEvaluator
// only ever evaluates against actual memberships — not touched here, so the Business Portal's
// preview/CampaignSweepWorker's real fan-out are both unaffected by this.
[Authorize]
[RemoteService(IsEnabled = false)]
public class CustomerCampaignAppService : ApplicationService, ICustomerCampaignAppService
{
    private readonly IRepository<Campaign, Guid> _campaignRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<Follow, Guid> _followRepository;
    private readonly IRepository<BusinessProfile, Guid> _businessProfileRepository;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly ICampaignSegmentEvaluator _segmentEvaluator;
    private readonly ICurrentUser _currentUser;
    private readonly IDataFilter _dataFilter;
    private readonly IClock _clock;

    public CustomerCampaignAppService(
        IRepository<Campaign, Guid> campaignRepository,
        IRepository<Membership, Guid> membershipRepository,
        IRepository<Follow, Guid> followRepository,
        IRepository<BusinessProfile, Guid> businessProfileRepository,
        IRepository<Tenant, Guid> tenantRepository,
        ICampaignSegmentEvaluator segmentEvaluator,
        ICurrentUser currentUser,
        IDataFilter dataFilter,
        IClock clock)
    {
        _campaignRepository = campaignRepository;
        _membershipRepository = membershipRepository;
        _followRepository = followRepository;
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
            // A campaign is only relevant for a business the customer belongs to — a promotion at a
            // business you have not joined (or have since left — MembershipStatus.Cancelled) isn't
            // yours. (Followed-only businesses are handled separately below.)
            var memberships = await _membershipRepository.GetListAsync(m =>
                m.CustomerId == customerId &&
                m.Status == MembershipStatus.Active &&
                (onlyTenantId == null || m.TenantId == onlyTenantId));

            var membershipIds = memberships.Select(m => m.Id).ToHashSet();
            var memberTenantIds = memberships
                .Where(m => m.TenantId.HasValue)
                .Select(m => m.TenantId!.Value)
                .ToHashSet();

            // Followed-but-not-joined businesses only ever contribute their broadly-targeted campaigns
            // — see this class's own file comment. Excludes any tenant already covered by membership
            // above, so a business the customer both follows and belongs to isn't evaluated twice.
            var follows = await _followRepository.GetListAsync(f =>
                f.CustomerId == customerId &&
                (onlyTenantId == null || f.TenantId == onlyTenantId));
            var followedOnlyTenantIds = follows
                .Where(f => f.TenantId.HasValue && !memberTenantIds.Contains(f.TenantId.Value))
                .Select(f => f.TenantId!.Value)
                .ToHashSet();

            var tenantIds = memberTenantIds.Union(followedOnlyTenantIds).ToList();

            if (tenantIds.Count == 0)
            {
                return new List<CustomerCampaignDto>();
            }

            // Suspended or Pending businesses should not be promoting anything.
            var approvedProfiles = (await _businessProfileRepository.GetListAsync(p =>
                    p.TenantId != null && tenantIds.Contains(p.TenantId.Value)))
                .Where(p => p.ApprovalStatus == TenantApprovalStatus.Approved)
                .ToList();
            var approvedTenantIds = approvedProfiles.Select(p => p.TenantId!.Value).ToHashSet();

            // Prefer the business's own chosen display name over the technical Tenant.Name — see
            // BusinessProfile.DisplayName's own comment.
            var displayNameByTenantId = approvedProfiles
                .Where(p => !p.DisplayName.IsNullOrWhiteSpace())
                .ToDictionary(p => p.TenantId!.Value, p => p.DisplayName!);

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
                var tenantId = campaign.TenantId!.Value;

                // Member: full segment evaluation, unchanged. Followed-only: no Membership to evaluate
                // a real segment against, so only the broadest campaigns (untargeted, or an explicit
                // "All" rule) are eligible — see IsBroadlyTargeted.
                var eligible = memberTenantIds.Contains(tenantId)
                    ? await TargetsCustomerAsync(campaign, membershipIds)
                    : IsBroadlyTargeted(campaign);

                if (!eligible)
                {
                    continue;
                }

                results.Add(new CustomerCampaignDto
                {
                    Id = campaign.Id,
                    TenantId = tenantId,
                    BusinessName = displayNameByTenantId.GetValueOrDefault(tenantId)
                        ?? tenantNames.GetValueOrDefault(tenantId)
                        ?? string.Empty,
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

    // For a followed-only (non-member) customer: true if the campaign isn't restricted to a
    // membership-based segment at all — either no target rules (untargeted = everyone, same as
    // TargetsCustomerAsync's own "no rules" shortcut) or an explicit All rule. Deliberately doesn't
    // call ICampaignSegmentEvaluator — that evaluator only ever works against real Membership rows,
    // which a follower doesn't have.
    private static bool IsBroadlyTargeted(Campaign campaign) =>
        campaign.TargetRules.Count == 0 ||
        campaign.TargetRules.Any(r => r.SegmentType == CampaignTargetRuleSegmentType.All);
}
