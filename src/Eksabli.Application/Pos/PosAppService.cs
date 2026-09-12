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

[RemoteService(IsEnabled = false)]
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
    // NOT IIdentityUserRepository — that's ABP's curated interface (no predicate/queryable access, per
    // AdminUserAppService's own comment on the same constraint) and can only look things up by id or by
    // NormalizedUserName/NormalizedEmail. A phone lookup needs to query the actual
    // IdentityUser.PhoneNumber column, so this needs the full generic repository instead.
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
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
        IRepository<IdentityUser, Guid> identityUserRepository,
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

        // Queries the real IdentityUser.PhoneNumber column directly — not UserName. UserName happens
        // to also be set to the same normalized phone number today (see OtpLoginService), but that's
        // an implementation detail of how a customer's account gets created, not something a phone
        // lookup should depend on; PhoneNumber is the actual, semantically-correct field, and this
        // stays correct even if that UserName convention ever changes. Still normalized the same way
        // on both sides (see PhoneNumberNormalizer's own comment) — a cashier typing a number in by
        // hand ("+966 50 111 2222") has to resolve to the same string as whatever the customer's own
        // app sent at registration ("+966501112222") for either field to match.
        var normalizedPhoneNumber = PhoneNumberNormalizer.Normalize(input.PhoneNumber);

        IdentityUser? user;
        using (_currentTenant.Change(null)) // Host-realm identity space, same shape as OtpLoginService
        {
            user = await _identityUserRepository.FirstOrDefaultAsync(u => u.PhoneNumber == normalizedPhoneNumber);
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

    // Read-only preview of a code, so staff can see WHO and WHAT before committing anything.
    //
    // Never moves points and never changes coupon state — a cashier scanning the wrong code, or
    // scanning twice while the customer fumbles their phone, must be free of consequence. That is also
    // why the role check here is advisory (reported as CanCurrentEmployeeApprove) rather than thrown:
    // a cashier holding a manager-only reward should be told to fetch a manager, not handed a 403 with
    // no explanation of what they were even looking at.
    public async Task<RedemptionLookupDto> LookupRedemptionAsync(LookupRedemptionDto input)
    {
        var (coupon, reward) = await ResolveOpenRedemptionAsync(input.Code, mayRelease: false);

        var requiresManager = RequiresManagerApproval(reward);
        var assignment = await _employeeAssignmentRepository.FirstOrDefaultAsync(a => a.UserId == CurrentUser.GetId());
        var canApprove = assignment != null && AllowedRolesFor(requiresManager).Contains(assignment.Role);

        var membership = await _membershipRepository.GetAsync(coupon.MembershipId);
        var wallet = await _walletRepository.FirstAsync(w => w.MembershipId == membership.Id);

        var (name, phone) = await ResolveCustomerIdentityAsync(membership.CustomerId);

        return new RedemptionLookupDto
        {
            CouponId = coupon.Id,
            Code = coupon.Code,
            RewardNameAr = reward.NameAr,
            RewardNameEn = reward.NameEn,
            PointsCost = coupon.PointsCost,
            CustomerName = name,
            CustomerPhone = phone,
            // Balance already excludes the hold via AvailableBalance, so this is what the customer
            // walks away with — no further subtraction, and no double-counting for a legacy Issued row
            // whose points left the wallet long ago.
            BalanceAfterRedemption = coupon.HoldsReservation ? wallet.AvailableBalance : wallet.Balance,
            IssuedAt = coupon.IssuedAt,
            ReservationExpiresAt = coupon.ReservationExpiresAt,
            RequiresManagerApproval = requiresManager,
            CanCurrentEmployeeApprove = canApprove
        };
    }

    // Staff approve: the hold becomes a real debit and the customer gets the reward.
    //
    // This is the ONLY place points actually leave a wallet for a redemption. CouponAppService.RedeemAsync
    // merely reserved them.
    public async Task<RedemptionConfirmationDto> ConfirmRedemptionAsync(ConfirmRedemptionDto input)
    {
        var (coupon, reward) = await ResolveOpenRedemptionAsync(input.Code, mayRelease: true);

        // Enforced here for real (unlike the advisory flag on lookup) — this is the state change.
        var allowedRoles = AllowedRolesFor(RequiresManagerApproval(reward));
        var (employeeId, defaultBranchId) = await CheckStaffRoleAsync(allowedRoles);

        var wallet = await _walletRepository.FirstAsync(w => w.MembershipId == coupon.MembershipId);
        var pointsDebited = 0;

        // A legacy `Issued` coupon was already paid for at creation time and holds no reservation, so
        // approving it hands over the reward without touching the wallet again. Charging it here would
        // bill the customer twice for one drink. See CouponStatus's own comment.
        if (coupon.HoldsReservation)
        {
            var transaction = PointsTransaction.Create(
                GuidGenerator.Create(),
                wallet.Id,
                PointsTransactionType.Redeem,
                -coupon.PointsCost,
                PointsTransactionSource.Reward,
                referenceId: coupon.Id,
                createdByEmployeeId: employeeId);
            await _transactionRepository.InsertAsync(transaction);

            // Order matters only for clarity, not correctness: the hold is dropped and the same amount
            // is taken off Balance, so AvailableBalance is unchanged across the pair — which is exactly
            // right, since the customer could never spend these points anyway.
            wallet.CommitReservation(coupon.PointsCost);
            wallet.ApplyTransaction(PointsTransactionType.Redeem, -coupon.PointsCost);
            await _walletRepository.UpdateAsync(wallet);

            pointsDebited = coupon.PointsCost;
        }

        coupon.Approve(Clock.Now, employeeId, input.BranchId ?? defaultBranchId);
        await _couponRepository.UpdateAsync(coupon);

        var membership = await _membershipRepository.GetAsync(coupon.MembershipId);
        var (name, _) = await ResolveCustomerIdentityAsync(membership.CustomerId);

        return new RedemptionConfirmationDto
        {
            CouponId = coupon.Id,
            RewardNameAr = reward.NameAr,
            RewardNameEn = reward.NameEn,
            RedeemedAt = coupon.RedeemedAt!.Value,
            PointsDebited = pointsDebited,
            NewBalance = wallet.Balance,
            CustomerName = name
        };
    }

    // Staff decline: the hold evaporates and the customer has their points back immediately.
    //
    // No ledger row is written, because no points ever moved — see PointsWallet.Reserved. Any staff
    // role may decline, including a Cashier holding a manager-only reward: refusing to hand something
    // over is never the privileged direction, and forcing them to find a manager just to give the
    // customer their points back would be perverse.
    public async Task<RedemptionRejectionDto> RejectRedemptionAsync(RejectRedemptionDto input)
    {
        var (coupon, reward) = await ResolveOpenRedemptionAsync(input.Code, mayRelease: true);

        if (!coupon.HoldsReservation)
        {
            throw new UserFriendlyException("This redemption is no longer awaiting approval.");
        }

        var (employeeId, _) = await CheckStaffRoleAsync(EmployeeRole.Owner, EmployeeRole.BranchManager, EmployeeRole.Cashier);

        coupon.Reject(employeeId, input.Reason);
        await _couponRepository.UpdateAsync(coupon);

        var wallet = await _walletRepository.FirstAsync(w => w.MembershipId == coupon.MembershipId);
        wallet.ReleaseReservation(coupon.PointsCost);
        await _walletRepository.UpdateAsync(wallet);

        reward.IncrementStock();
        await _rewardRepository.UpdateAsync(reward);

        return new RedemptionRejectionDto
        {
            CouponId = coupon.Id,
            RewardNameAr = reward.NameAr,
            RewardNameEn = reward.NameEn,
            PointsReleased = coupon.PointsCost,
            NewAvailableBalance = wallet.AvailableBalance
        };
    }

    // Shared front half of all three redemption endpoints: find the coupon, prove it is still open,
    // and load its reward. Kept in one place so a code that is expired/used/unknown produces the same
    // message whichever button staff pressed.
    //
    // `mayRelease` exists because one of those checks can legitimately end a redemption. If the REWARD
    // itself has expired since the customer reserved it, the coupon is dead and its hold should go back
    // immediately rather than sitting until RedemptionReservationWorker notices. But this same method
    // serves the read-only lookup, and a lookup must never move points — a cashier scanning a code to
    // see what it is has not decided anything yet. So the mutating callers opt in, and the lookup does
    // not; for the lookup the hold simply lapses on its normal schedule.
    private async Task<(Coupon Coupon, Reward Reward)> ResolveOpenRedemptionAsync(string code, bool mayRelease)
    {
        // Codes are typed as often as they are scanned, and a cashier reading one off a phone screen
        // will not match the stored casing or the spacing the customer app renders it with.
        var normalized = (code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty).ToUpperInvariant();

        var coupon = await _couponRepository.FirstOrDefaultAsync(c => c.Code == normalized);
        if (coupon == null)
        {
            throw new UserFriendlyException("Invalid or already-used code.");
        }

        if (coupon.Status != CouponStatus.Pending && coupon.Status != CouponStatus.Issued)
        {
            throw new UserFriendlyException("Invalid or already-used code.");
        }

        // Lapsed but not yet swept: the worker runs every five minutes, so a code can be past its
        // window and still Pending here. Refuse it either way; only a mutating caller cleans it up.
        if (coupon.HasLapsed(Clock.Now))
        {
            if (mayRelease)
            {
                await ExpireAndReleaseAsync(coupon);
            }

            throw new UserFriendlyException("This redemption has expired. Ask the customer to start it again.");
        }

        var reward = await _rewardRepository.GetAsync(coupon.RewardId);

        if (reward.ValidTo.HasValue && reward.ValidTo.Value < Clock.Now)
        {
            if (mayRelease)
            {
                await ExpireAndReleaseAsync(coupon, reward);
            }

            throw new UserFriendlyException("This reward offer has expired.");
        }

        return (coupon, reward);
    }

    // Ends a dead redemption and undoes everything it was holding. Safe to call for a legacy `Issued`
    // coupon, which holds nothing — HoldsReservation guards both the wallet and the stock.
    private async Task ExpireAndReleaseAsync(Coupon coupon, Reward? reward = null)
    {
        var held = coupon.HoldsReservation;

        coupon.MarkExpired();
        await _couponRepository.UpdateAsync(coupon);

        if (!held)
        {
            return;
        }

        var wallet = await _walletRepository.FirstAsync(w => w.MembershipId == coupon.MembershipId);
        wallet.ReleaseReservation(coupon.PointsCost);
        await _walletRepository.UpdateAsync(wallet);

        reward ??= await _rewardRepository.FirstOrDefaultAsync(r => r.Id == coupon.RewardId);
        if (reward != null)
        {
            reward.IncrementStock();
            await _rewardRepository.UpdateAsync(reward);
        }
    }

    // High-value rewards (per-reward, tenant-configured threshold) require Manager+, not just Cashier.
    private static bool RequiresManagerApproval(Reward reward) =>
        reward.ApprovalThresholdPoints.HasValue && reward.PointsCost >= reward.ApprovalThresholdPoints.Value;

    private static EmployeeRole[] AllowedRolesFor(bool requiresManager) =>
        requiresManager
            ? new[] { EmployeeRole.Owner, EmployeeRole.BranchManager }
            : new[] { EmployeeRole.Owner, EmployeeRole.BranchManager, EmployeeRole.Cashier };

    // Name comes from the CustomerProfile, phone from the IdentityUser — the two live in different
    // places, and a customer who signed up by OTP without completing a profile has only the latter.
    private async Task<(string? Name, string? Phone)> ResolveCustomerIdentityAsync(Guid customerId)
    {
        var profile = await _customerProfileRepository.FirstOrDefaultAsync(p => p.UserId == customerId);
        var user = await _identityUserRepository.FirstOrDefaultAsync(u => u.Id == customerId);

        var name = profile == null
            ? null
            : $"{profile.FirstName} {profile.LastName}".Trim();

        return (string.IsNullOrWhiteSpace(name) ? null : name, user?.PhoneNumber);
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
