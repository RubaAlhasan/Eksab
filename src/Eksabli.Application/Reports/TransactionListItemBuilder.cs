using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.Branches;
using Eksabli.Campaigns;
using Eksabli.CustomerProfiles;
using Eksabli.EmployeeAssignments;
using Eksabli.Engagement;
using Eksabli.Memberships;
using Eksabli.Rewards;
using Eksabli.Wallets;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Reports;

// "Raw PointsTransaction rows -> one TransactionListItemDto per real-world process" resolver, extracted
// from ReportsAppService.GetTransactionsListAsync so AdminUserAppService.GetCustomerTransactionsAsync
// can show the exact same Business Portal breakdown (grouped-by-BatchId rows, resolved Branch/Staff/
// ReferenceName) instead of a second, poorer copy of this logic against the plain PointsTransactionDto.
public class TransactionListItemBuilder : ITransientDependency
{
    private readonly IPointsTransactionRepository _transactionRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<CustomerProfile, Guid> _customerProfileRepository;
    private readonly IRepository<EmployeeAssignment, Guid> _employeeAssignmentRepository;
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly IRepository<Branch, Guid> _branchRepository;
    private readonly IRepository<Wallets.Tier, Guid> _tierRepository;
    private readonly IRepository<Campaign, Guid> _campaignRepository;
    private readonly IRepository<Coupon, Guid> _couponRepository;
    private readonly IRepository<Reward, Guid> _rewardRepository;
    private readonly IRepository<Referral, Guid> _referralRepository;
    private readonly ICurrentTenant _currentTenant;

    public TransactionListItemBuilder(
        IPointsTransactionRepository transactionRepository,
        IRepository<PointsWallet, Guid> walletRepository,
        IRepository<Membership, Guid> membershipRepository,
        IRepository<CustomerProfile, Guid> customerProfileRepository,
        IRepository<EmployeeAssignment, Guid> employeeAssignmentRepository,
        IIdentityUserRepository identityUserRepository,
        IRepository<Branch, Guid> branchRepository,
        IRepository<Wallets.Tier, Guid> tierRepository,
        IRepository<Campaign, Guid> campaignRepository,
        IRepository<Coupon, Guid> couponRepository,
        IRepository<Reward, Guid> rewardRepository,
        IRepository<Referral, Guid> referralRepository,
        ICurrentTenant currentTenant)
    {
        _transactionRepository = transactionRepository;
        _walletRepository = walletRepository;
        _membershipRepository = membershipRepository;
        _customerProfileRepository = customerProfileRepository;
        _employeeAssignmentRepository = employeeAssignmentRepository;
        _identityUserRepository = identityUserRepository;
        _branchRepository = branchRepository;
        _tierRepository = tierRepository;
        _campaignRepository = campaignRepository;
        _couponRepository = couponRepository;
        _rewardRepository = rewardRepository;
        _referralRepository = referralRepository;
        _currentTenant = currentTenant;
    }

    // `page` is assumed already filtered/sorted/paged by the caller (bounded by page size) — every
    // lookup here is scoped to exactly the ids that page needs, same "only resolve display data for
    // this page's rows" shape ReportsAppService.GetTransactionsListAsync already documented.
    public async Task<List<TransactionListItemDto>> BuildAsync(List<PointsTransaction> page)
    {
        if (page.Count == 0)
        {
            return new List<TransactionListItemDto>();
        }

        // Rows with no staff attribution (customer/system-triggered) or a staff member with all-branch
        // access (BranchId null) simply have no resolvable branch.
        var employeeIds = page.Where(t => t.CreatedByEmployeeId.HasValue).Select(t => t.CreatedByEmployeeId!.Value).Distinct().ToList();
        var employeeBranchLookup = employeeIds.Count == 0
            ? new Dictionary<Guid, Guid?>()
            : (await _employeeAssignmentRepository.GetListAsync(e => employeeIds.Contains(e.UserId)))
                .ToDictionary(e => e.UserId, e => e.BranchId);

        var staffEmailLookup = new Dictionary<Guid, string>();
        foreach (var staffId in employeeIds)
        {
            var staffUser = await _identityUserRepository.FindAsync(staffId);
            if (staffUser != null)
            {
                staffEmailLookup[staffId] = staffUser.Email;
            }
        }

        var branchIdsForNames = employeeBranchLookup.Values.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var branchNameLookup = branchIdsForNames.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _branchRepository.GetListAsync(b => branchIdsForNames.Contains(b.Id))).ToDictionary(b => b.Id, b => b.Name);

        // ReferenceId is polymorphic per Source (see PointsTransaction's own comment) — an Expire row's
        // ReferenceId points at the earn transaction it reverses, not directly at a Tier/Campaign/Coupon,
        // so its "real" reference has to be resolved one hop further via that original row.
        var expireOriginalIds = page.Where(t => t.Type == PointsTransactionType.Expire && t.ReferenceId.HasValue)
            .Select(t => t.ReferenceId!.Value).Distinct().ToList();
        var expireOriginals = expireOriginalIds.Count == 0
            ? new Dictionary<Guid, PointsTransaction>()
            : (await _transactionRepository.GetListAsync(t => expireOriginalIds.Contains(t.Id))).ToDictionary(t => t.Id);

        Guid? ResolveReferenceId(PointsTransaction t) =>
            t.Type == PointsTransactionType.Expire && t.ReferenceId.HasValue && expireOriginals.TryGetValue(t.ReferenceId.Value, out var original)
                ? original.ReferenceId
                : t.ReferenceId;

        var tierIds = page.Where(t => t.Source == PointsTransactionSource.Tier)
            .Select(ResolveReferenceId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var tierNameLookup = tierIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _tierRepository.GetListAsync(t => tierIds.Contains(t.Id))).ToDictionary(t => t.Id, t => t.Name);

        var campaignIds = page.Where(t => t.Source == PointsTransactionSource.Campaign || t.Source == PointsTransactionSource.Birthday)
            .Select(ResolveReferenceId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var campaignNameLookup = campaignIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _campaignRepository.GetListAsync(c => campaignIds.Contains(c.Id))).ToDictionary(c => c.Id, c => c.NameEn);

        var couponIds = page.Where(t => t.Source == PointsTransactionSource.Reward)
            .Select(ResolveReferenceId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var couponLookup = couponIds.Count == 0
            ? new Dictionary<Guid, Coupon>()
            : (await _couponRepository.GetListAsync(c => couponIds.Contains(c.Id))).ToDictionary(c => c.Id);
        var rewardIds = couponLookup.Values.Select(c => c.RewardId).Distinct().ToList();
        var rewardNameLookup = rewardIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _rewardRepository.GetListAsync(r => rewardIds.Contains(r.Id))).ToDictionary(r => r.Id, r => r.NameEn);

        var referralIds = page.Where(t => t.Source == PointsTransactionSource.Referral)
            .Select(ResolveReferenceId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var referralLookup = referralIds.Count == 0
            ? new Dictionary<Guid, Referral>()
            : (await _referralRepository.GetListAsync(r => referralIds.Contains(r.Id))).ToDictionary(r => r.Id);

        var refereeCustomerIds = referralLookup.Values.Select(r => r.RefereeCustomerId).Distinct().ToList();
        Dictionary<Guid, CustomerProfile> refereeProfileLookup;
        using (_currentTenant.Change(null)) // CustomerProfile is Host-realm
        {
            refereeProfileLookup = refereeCustomerIds.Count == 0
                ? new Dictionary<Guid, CustomerProfile>()
                : (await _customerProfileRepository.GetListAsync(p => refereeCustomerIds.Contains(p.UserId))).ToDictionary(p => p.UserId);
        }

        string? ResolveReferenceName(PointsTransaction t)
        {
            var refId = ResolveReferenceId(t);
            if (!refId.HasValue)
            {
                return null;
            }

            switch (t.Source)
            {
                case PointsTransactionSource.Tier:
                    return tierNameLookup.GetValueOrDefault(refId.Value);
                case PointsTransactionSource.Campaign:
                case PointsTransactionSource.Birthday:
                    return campaignNameLookup.GetValueOrDefault(refId.Value);
                case PointsTransactionSource.Reward:
                    return couponLookup.TryGetValue(refId.Value, out var coupon)
                        ? rewardNameLookup.GetValueOrDefault(coupon.RewardId)
                        : null;
                case PointsTransactionSource.Referral:
                    if (referralLookup.TryGetValue(refId.Value, out var referral) &&
                        refereeProfileLookup.TryGetValue(referral.RefereeCustomerId, out var refereeProfile))
                    {
                        return $"{refereeProfile.FirstName} {refereeProfile.LastName}".Trim();
                    }
                    return null;
                default:
                    return null;
            }
        }

        var walletIds = page.Select(t => t.WalletId).Distinct().ToList();
        var walletToMembership = (await _walletRepository.GetListAsync(w => walletIds.Contains(w.Id)))
            .ToDictionary(w => w.Id, w => w.MembershipId);

        var membershipIds = walletToMembership.Values.Distinct().ToList();
        var membershipToCustomer = (await _membershipRepository.GetListAsync(m => membershipIds.Contains(m.Id)))
            .ToDictionary(m => m.Id, m => m.CustomerId);

        Dictionary<Guid, CustomerProfile> profileLookup;
        using (_currentTenant.Change(null)) // CustomerProfile is Host-realm
        {
            var customerIds = membershipToCustomer.Values.Distinct().ToList();
            profileLookup = (await _customerProfileRepository.GetListAsync(p => customerIds.Contains(p.UserId)))
                .ToDictionary(p => p.UserId);
        }

        // Group the raw rows back into one row per real-world process (see PointsTransaction.BatchId /
        // PosAppService.AwardPointsCoreAsync) — a row with no BatchId is its own singleton group via the
        // `?? t.Id` fallback. GroupBy preserves each group's first-occurrence position, so if `page`
        // arrives newest-first, the grouped order stays newest-first too, with no re-sort needed.
        return page
            .GroupBy(t => t.BatchId ?? t.Id)
            .Select(group =>
            {
                var rows = group.ToList();
                var primary = rows.FirstOrDefault(t => t.Source == PointsTransactionSource.Purchase) ?? rows[0];

                Guid? customerId = null;
                CustomerProfile? profile = null;
                if (walletToMembership.TryGetValue(primary.WalletId, out var membershipId) &&
                    membershipToCustomer.TryGetValue(membershipId, out var custId))
                {
                    customerId = custId;
                    profileLookup.TryGetValue(custId, out profile);
                }

                Guid? branchId = null;
                if (primary.CreatedByEmployeeId.HasValue)
                {
                    employeeBranchLookup.TryGetValue(primary.CreatedByEmployeeId.Value, out branchId);
                }

                return new TransactionListItemDto
                {
                    Id = primary.Id,
                    CustomerId = customerId,
                    CustomerFirstName = profile?.FirstName,
                    CustomerLastName = profile?.LastName,
                    Type = primary.Type,
                    Points = rows.Sum(t => t.Points),
                    Source = primary.Source,
                    BranchId = branchId,
                    StaffId = primary.CreatedByEmployeeId,
                    BranchName = branchId.HasValue ? branchNameLookup.GetValueOrDefault(branchId.Value) : null,
                    StaffEmail = primary.CreatedByEmployeeId.HasValue ? staffEmailLookup.GetValueOrDefault(primary.CreatedByEmployeeId.Value) : null,
                    CreationTime = primary.CreationTime,
                    Components = rows.Select(t => new TransactionComponentDto
                    {
                        Id = t.Id,
                        Source = t.Source,
                        Points = t.Points,
                        ReferenceName = ResolveReferenceName(t),
                        TierMultiplier = t.TierMultiplierSnapshot,
                        Reason = t.Reason,
                        Amount = t.Amount,
                        Currency = t.Currency
                    }).ToList()
                };
            }).ToList();
    }
}
