using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Eksabli.Campaigns;
using Eksabli.CustomerProfiles;
using Eksabli.EmployeeAssignments;
using Eksabli.Engagement;
using Eksabli.Memberships;
using Eksabli.Rewards;
using Eksabli.Wallets;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace Eksabli.Pos;

public class PosAppService : ApplicationService, IPosAppService
{
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<PointsTransaction, Guid> _transactionRepository;
    private readonly IRepository<Wallets.Tier, Guid> _tierRepository;
    private readonly IRepository<PointRule, Guid> _pointRuleRepository;
    private readonly IRepository<EmployeeAssignment, Guid> _employeeAssignmentRepository;
    private readonly IRepository<CustomerProfile, Guid> _customerProfileRepository;
    private readonly ICouponRepository _couponRepository;
    private readonly IRewardRepository _rewardRepository;
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDistributedCache _qrCache;
    private readonly ICampaignRulesEngine _campaignRulesEngine;
    private readonly IReferralCompletionService _referralCompletionService;
    private readonly ITierRecomputeService _tierRecomputeService;

    public PosAppService(
        IRepository<Membership, Guid> membershipRepository,
        IRepository<PointsWallet, Guid> walletRepository,
        IRepository<PointsTransaction, Guid> transactionRepository,
        IRepository<Wallets.Tier, Guid> tierRepository,
        IRepository<PointRule, Guid> pointRuleRepository,
        IRepository<EmployeeAssignment, Guid> employeeAssignmentRepository,
        IRepository<CustomerProfile, Guid> customerProfileRepository,
        ICouponRepository couponRepository,
        IRewardRepository rewardRepository,
        IIdentityUserRepository identityUserRepository,
        ICurrentTenant currentTenant,
        IDistributedCache qrCache,
        ICampaignRulesEngine campaignRulesEngine,
        IReferralCompletionService referralCompletionService,
        ITierRecomputeService tierRecomputeService)
    {
        _membershipRepository = membershipRepository;
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _tierRepository = tierRepository;
        _pointRuleRepository = pointRuleRepository;
        _employeeAssignmentRepository = employeeAssignmentRepository;
        _customerProfileRepository = customerProfileRepository;
        _couponRepository = couponRepository;
        _rewardRepository = rewardRepository;
        _identityUserRepository = identityUserRepository;
        _currentTenant = currentTenant;
        _qrCache = qrCache;
        _campaignRulesEngine = campaignRulesEngine;
        _referralCompletionService = referralCompletionService;
        _tierRecomputeService = tierRecomputeService;
    }

    public async Task<CustomerLookupResultDto> LookupCustomerByPhoneAsync(PhoneLookupDto input)
    {
        await CheckStaffRoleAsync(EmployeeRole.Owner, EmployeeRole.BranchManager, EmployeeRole.Cashier);

        IdentityUser? user;
        using (_currentTenant.Change(null)) // Host-realm identity space, same shape as OtpLoginService
        {
            user = await _identityUserRepository.FindByNormalizedUserNameAsync(input.PhoneNumber.ToUpperInvariant());
        }

        // Ambient tenant is still the caller's own tenant here — Membership's IMultiTenant filter
        // already scopes this lookup to "this tenant only".
        var membership = user == null
            ? null
            : await _membershipRepository.FirstOrDefaultAsync(m => m.CustomerId == user.Id);

        if (user == null || membership == null)
        {
            // Deliberately identical exception for both cases — don't leak "is this phone number an
            // Eksabli customer at all" to staff at a business this customer hasn't joined.
            throw new UserFriendlyException("No matching customer found.");
        }

        var wallet = await _walletRepository.FirstAsync(w => w.MembershipId == membership.Id);

        CustomerProfile? profile;
        using (_currentTenant.Change(null))
        {
            profile = await _customerProfileRepository.FirstOrDefaultAsync(p => p.UserId == user.Id);
        }

        return new CustomerLookupResultDto
        {
            CustomerId = user.Id,
            MembershipId = membership.Id,
            WalletId = wallet.Id,
            Balance = wallet.Balance,
            FirstName = profile?.FirstName,
            LastName = profile?.LastName
        };
    }

    public async Task<AwardPointsResultDto> AwardPointsByQrAsync(AwardPointsByQrDto input)
    {
        var cacheKey = WalletQrCacheItem.CacheKeyPrefix + input.QrToken;
        var bytes = await _qrCache.GetAsync(cacheKey)
            ?? throw new AbpAuthorizationException("Invalid or expired wallet QR token.");
        await _qrCache.RemoveAsync(cacheKey); // single-use — burn on any successful read

        var cached = JsonSerializer.Deserialize<WalletQrCacheItem>(bytes)!;
        return await AwardPointsCoreAsync(cached.CustomerId, input.PurchaseAmount);
    }

    public async Task<AwardPointsResultDto> AwardPointsByCustomerIdAsync(Guid customerId, AwardPointsByCustomerIdDto input)
    {
        return await AwardPointsCoreAsync(customerId, input.PurchaseAmount);
    }

    public async Task<AwardPointsResultDto> ManualAdjustAsync(ManualAdjustDto input)
    {
        var (employeeId, _) = await CheckStaffRoleAsync(EmployeeRole.Owner, EmployeeRole.BranchManager);

        var todayStartUtc = Clock.Now.Date;
        var todaysCount = await _transactionRepository.CountAsync(t =>
            t.CreatedByEmployeeId == employeeId &&
            t.Type == PointsTransactionType.Adjust &&
            t.CreationTime >= todayStartUtc);

        if (todaysCount >= PointsTransactionConsts.MaxDailyManualAdjustmentsPerEmployee)
        {
            throw new UserFriendlyException("You've reached today's limit for manual point adjustments.");
        }

        var membership = await _membershipRepository.FirstOrDefaultAsync(m => m.CustomerId == input.CustomerId)
            ?? throw new UserFriendlyException("This customer hasn't joined your business yet.");
        var wallet = await _walletRepository.FirstAsync(w => w.MembershipId == membership.Id);

        var transaction = PointsTransaction.Create(
            GuidGenerator.Create(),
            wallet.Id,
            PointsTransactionType.Adjust,
            input.Points,
            PointsTransactionSource.Manual,
            createdByEmployeeId: employeeId,
            reason: input.Reason);
        await _transactionRepository.InsertAsync(transaction);

        wallet.ApplyTransaction(PointsTransactionType.Adjust, input.Points);
        await _walletRepository.UpdateAsync(wallet);

        return await BuildResultAsync(transaction, wallet);
    }

    // Private helper, not a manager service — pipeline/ledger/tier-recompute logic shared by both
    // award paths (QR and phone/customer-id) so it isn't duplicated.
    private async Task<AwardPointsResultDto> AwardPointsCoreAsync(Guid customerId, decimal? purchaseAmount)
    {
        await CheckStaffRoleAsync(EmployeeRole.Owner, EmployeeRole.BranchManager, EmployeeRole.Cashier);

        var membership = await _membershipRepository.FirstOrDefaultAsync(m => m.CustomerId == customerId)
            ?? throw new UserFriendlyException("This customer hasn't joined your business yet.");

        var wallet = await _walletRepository.FirstAsync(w => w.MembershipId == membership.Id);

        decimal tierMultiplier = 1.0m;
        if (wallet.CurrentTierId.HasValue)
        {
            var tier = await _tierRepository.FindAsync(wallet.CurrentTierId.Value);
            if (tier != null)
            {
                tierMultiplier = tier.Multiplier;
            }
        }

        var points = await ComputePointsAsync(purchaseAmount, tierMultiplier);
        var isFirstEarn = wallet.LifetimeEarned == 0; // the qualifying action for referral completion

        var transaction = PointsTransaction.Create(
            GuidGenerator.Create(),
            wallet.Id,
            PointsTransactionType.Earn,
            points,
            PointsTransactionSource.Purchase,
            tierMultiplierSnapshot: tierMultiplier);
        await _transactionRepository.InsertAsync(transaction);

        wallet.ApplyTransaction(PointsTransactionType.Earn, points);
        await _tierRecomputeService.RecomputeAsync(wallet);
        await _walletRepository.UpdateAsync(wallet);

        // Feature 06's referral bonus — awarded only on the referee's actual first purchase, not just
        // signup. See docs/eksabli-loyalty-platform/features/06-engagement-gamification/README.md.
        await _referralCompletionService.TryCompleteAsync(membership, wallet, isFirstEarn);

        return await BuildResultAsync(transaction, wallet);
    }

    // Points pipeline: base rule × tier multiplier × campaign multiplier, plus any flat SpendXGetY
    // bonus — the real-time evaluation mode from
    // docs/eksabli-loyalty-platform/features/05-campaigns-notifications/README.md#business-rules,
    // evaluated inline via ICampaignRulesEngine (DoublePoints/SpendXGetY campaigns only; the *other*
    // mode — Birthday/WinBack/Vip/NewCustomer — runs as a batch sweep in Campaigns.CampaignSweepWorker).
    // Rounding: floor, applied once to the multiplied portion — see
    // docs/eksabli-loyalty-platform/07-loyalty-engine.md#8.
    private async Task<int> ComputePointsAsync(decimal? purchaseAmount, decimal tierMultiplier)
    {
        decimal basePoints = 0m;

        if (purchaseAmount.HasValue)
        {
            var rule = await _pointRuleRepository.FirstOrDefaultAsync(r => r.RuleType == PointRuleType.PerCurrencyUnit);
            if (rule != null)
            {
                basePoints = purchaseAmount.Value * rule.PointsPerUnit;
            }
        }

        if (basePoints == 0m)
        {
            var rule = await _pointRuleRepository.FirstOrDefaultAsync(r => r.RuleType == PointRuleType.PerVisit);
            if (rule != null)
            {
                basePoints = rule.PointsPerUnit;
            }
        }

        var campaignResult = await _campaignRulesEngine.EvaluateAsync(purchaseAmount);

        return (int)Math.Floor(basePoints * tierMultiplier * campaignResult.Multiplier) + campaignResult.BonusPoints;
    }

    private async Task<AwardPointsResultDto> BuildResultAsync(PointsTransaction transaction, PointsWallet wallet)
    {
        string? tierName = null;
        if (wallet.CurrentTierId.HasValue)
        {
            var tier = await _tierRepository.FindAsync(wallet.CurrentTierId.Value);
            tierName = tier?.Name;
        }

        return new AwardPointsResultDto
        {
            TransactionId = transaction.Id,
            PointsAwarded = transaction.Points,
            NewBalance = wallet.Balance,
            NewTierId = wallet.CurrentTierId,
            NewTierName = tierName
        };
    }

    public async Task<RedemptionConfirmationDto> ConfirmRedemptionAsync(ConfirmRedemptionDto input)
    {
        var coupon = await _couponRepository.FirstOrDefaultAsync(c => c.Code == input.Code);
        if (coupon == null || coupon.Status != CouponStatus.Issued)
        {
            throw new UserFriendlyException("Invalid or already-used code.");
        }

        var reward = await _rewardRepository.GetAsync(coupon.RewardId);

        if (reward.ValidTo.HasValue && reward.ValidTo.Value < Clock.Now)
        {
            coupon.MarkExpired();
            await _couponRepository.UpdateAsync(coupon);
            throw new UserFriendlyException("This reward offer has expired.");
        }

        // High-value rewards (per-reward, tenant-configured threshold) require Manager+, not just Cashier.
        var allowedRoles = reward.ApprovalThresholdPoints.HasValue && reward.PointsCost >= reward.ApprovalThresholdPoints.Value
            ? new[] { EmployeeRole.Owner, EmployeeRole.BranchManager }
            : new[] { EmployeeRole.Owner, EmployeeRole.BranchManager, EmployeeRole.Cashier };

        var (employeeId, defaultBranchId) = await CheckStaffRoleAsync(allowedRoles);

        coupon.MarkRedeemed(Clock.Now, employeeId, input.BranchId ?? defaultBranchId);
        await _couponRepository.UpdateAsync(coupon);

        return new RedemptionConfirmationDto
        {
            CouponId = coupon.Id,
            RewardNameAr = reward.NameAr,
            RewardNameEn = reward.NameEn,
            RedeemedAt = coupon.RedeemedAt!.Value
        };
    }

    // Role-hierarchy check — NOT an ABP permission check. No invited employee has any ABP permission
    // grant today (only the seeded tenant Owner does), so gating this with [Authorize(...)] would lock
    // out every real Cashier. Mirrors DeviceAppService.RemoveAsync's ownership-check shape.
    private async Task<(Guid EmployeeId, Guid? BranchId)> CheckStaffRoleAsync(params EmployeeRole[] allowedRoles)
    {
        var userId = CurrentUser.GetId();
        var assignment = await _employeeAssignmentRepository.FirstOrDefaultAsync(a => a.UserId == userId)
            ?? throw new AbpAuthorizationException("You are not staff at this business.");

        if (!allowedRoles.Contains(assignment.Role))
        {
            throw new AbpAuthorizationException("Your role does not permit this action.");
        }

        return (userId, assignment.BranchId);
    }
}
