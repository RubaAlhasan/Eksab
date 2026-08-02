using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.CustomerProfiles;
using Eksabli.Memberships;
using Eksabli.Wallets;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace Eksabli.Campaigns;

public class CampaignSegmentEvaluator : ICampaignSegmentEvaluator, ITransientDependency
{
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<PointsTransaction, Guid> _transactionRepository;
    private readonly IRepository<CustomerProfile, Guid> _customerProfileRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;

    public CampaignSegmentEvaluator(
        IRepository<Membership, Guid> membershipRepository,
        IRepository<PointsWallet, Guid> walletRepository,
        IRepository<PointsTransaction, Guid> transactionRepository,
        IRepository<CustomerProfile, Guid> customerProfileRepository,
        ICurrentTenant currentTenant,
        IClock clock)
    {
        _membershipRepository = membershipRepository;
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _customerProfileRepository = customerProfileRepository;
        _currentTenant = currentTenant;
        _clock = clock;
    }

    public async Task<List<Membership>> EvaluateAsync(Campaign campaign)
    {
        var activeMemberships = await _membershipRepository.GetListAsync(m => m.Status == MembershipStatus.Active);

        if (campaign.Type == CampaignType.Birthday)
        {
            var rules = CampaignRules.Parse(campaign.RulesJson);
            return await FilterByBirthdayAsync(activeMemberships, rules.DaysBefore ?? 3);
        }

        if (campaign.TargetRules.Count == 0)
        {
            // No explicit segment and not a Birthday campaign — targeting must be declared, not implied.
            return new List<Membership>();
        }

        var matchedIds = new HashSet<Guid>();
        var matched = new List<Membership>();
        foreach (var rule in campaign.TargetRules)
        {
            foreach (var membership in await EvaluateRuleAsync(activeMemberships, rule))
            {
                if (matchedIds.Add(membership.Id))
                {
                    matched.Add(membership);
                }
            }
        }

        return matched;
    }

    private async Task<List<Membership>> EvaluateRuleAsync(List<Membership> candidates, CampaignTargetRule rule)
    {
        var parameters = CampaignSegmentParameters.Parse(rule.ParametersJson);

        switch (rule.SegmentType)
        {
            case CampaignTargetRuleSegmentType.All:
                return candidates;

            case CampaignTargetRuleSegmentType.NewCustomer:
                var newCustomerCutoff = _clock.Now.AddDays(-(parameters.WithinDays ?? 7));
                return candidates.Where(m => m.JoinedAt >= newCustomerCutoff).ToList();

            case CampaignTargetRuleSegmentType.Tier:
                if (!parameters.TierId.HasValue)
                {
                    return new List<Membership>();
                }

                var walletsByMembershipForTier = await GetWalletsByMembershipAsync(candidates);
                return candidates.Where(m =>
                        walletsByMembershipForTier.TryGetValue(m.Id, out var wallet) &&
                        wallet.CurrentTierId == parameters.TierId.Value)
                    .ToList();

            case CampaignTargetRuleSegmentType.Inactive:
                var inactiveCutoff = _clock.Now.AddDays(-(parameters.InactiveDays ?? 30));
                var walletsByMembershipForInactive = await GetWalletsByMembershipAsync(candidates);
                var walletIds = walletsByMembershipForInactive.Values.Select(w => w.Id).ToList();
                var recentlyEarnedWalletIds = (await _transactionRepository.GetListAsync(t =>
                        walletIds.Contains(t.WalletId) &&
                        t.Type == PointsTransactionType.Earn &&
                        t.CreationTime >= inactiveCutoff))
                    .Select(t => t.WalletId)
                    .ToHashSet();
                return candidates.Where(m =>
                        walletsByMembershipForInactive.TryGetValue(m.Id, out var wallet) &&
                        !recentlyEarnedWalletIds.Contains(wallet.Id))
                    .ToList();

            default:
                return new List<Membership>();
        }
    }

    private async Task<Dictionary<Guid, PointsWallet>> GetWalletsByMembershipAsync(List<Membership> memberships)
    {
        var membershipIds = memberships.Select(m => m.Id).ToList();
        var wallets = await _walletRepository.GetListAsync(w => membershipIds.Contains(w.MembershipId));
        return wallets.ToDictionary(w => w.MembershipId);
    }

    private async Task<List<Membership>> FilterByBirthdayAsync(List<Membership> memberships, int daysBefore)
    {
        var customerIds = memberships.Select(m => m.CustomerId).ToList();

        List<CustomerProfile> profiles;
        using (_currentTenant.Change(null)) // CustomerProfile is Host-realm
        {
            profiles = await _customerProfileRepository.GetListAsync(p =>
                customerIds.Contains(p.UserId) && p.DateOfBirth != null);
        }

        var birthdaysByCustomerId = profiles.ToDictionary(p => p.UserId, p => p.DateOfBirth!.Value);
        var today = _clock.Now.Date;

        return memberships.Where(m =>
                birthdaysByCustomerId.TryGetValue(m.CustomerId, out var dateOfBirth) &&
                IsBirthdayWithinDays(dateOfBirth, today, daysBefore))
            .ToList();
    }

    private static bool IsBirthdayWithinDays(DateTime dateOfBirth, DateTime today, int daysBefore)
    {
        for (var offset = 0; offset <= daysBefore; offset++)
        {
            var candidate = today.AddDays(offset);
            if (dateOfBirth.Month == candidate.Month && dateOfBirth.Day == candidate.Day)
            {
                return true;
            }
        }

        return false;
    }
}
