using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.Memberships;
using Eksabli.Wallets;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace Eksabli.Rewards;

[RemoteService(IsEnabled = false)]
public class CouponAppService : ApplicationService, ICouponAppService
{
    private const int MaxCodeGenerationAttempts = 5;

    private readonly IRewardRepository _rewardRepository;
    private readonly ICouponRepository _couponRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    public CouponAppService(
        IRewardRepository rewardRepository,
        ICouponRepository couponRepository,
        IRepository<Membership, Guid> membershipRepository,
        IRepository<PointsWallet, Guid> walletRepository,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter)
    {
        _rewardRepository = rewardRepository;
        _couponRepository = couponRepository;
        _membershipRepository = membershipRepository;
        _walletRepository = walletRepository;
        _currentTenant = currentTenant;
        _dataFilter = dataFilter;
    }

    public async Task<PagedResultDto<RewardDto>> GetCatalogAsync(Guid tenantId, PagedAndSortedResultRequestDto input)
    {
        using (_currentTenant.Change(tenantId))
        {
            var (rewards, totalCount) = await _rewardRepository.GetListAsync(
                activeOnly: true,
                sorting: input.Sorting,
                skipCount: input.SkipCount,
                maxResultCount: input.MaxResultCount);

            return new PagedResultDto<RewardDto>(totalCount, ObjectMapper.Map<List<Reward>, List<RewardDto>>(rewards));
        }
    }

    // Opens a redemption; it does NOT complete one.
    //
    // The points are RESERVED, not debited, and the coupon is born Pending. Nothing here is worth
    // anything to the customer until a staff member approves the returned code at the counter
    // (PosAppService.ConfirmRedemptionAsync), and if nobody does, the hold is released — by staff
    // declining, by the customer cancelling, or by RedemptionReservationWorker sweeping the window.
    //
    // This is the whole reason the flow is two-phase: the customer decides *at home*, hours before
    // they reach the till, and debiting there would spend their points on a reward the branch might be
    // out of, closed for, or unable to authorise.
    public async Task<CouponDto> RedeemAsync(RedeemRewardDto input)
    {
        var customerId = CurrentUser.GetId();

        using (_currentTenant.Change(input.TenantId))
        {
            var membership = await _membershipRepository.FirstOrDefaultAsync(m => m.CustomerId == customerId)
                ?? throw new UserFriendlyException("You haven't joined this business yet.");

            var reward = await _rewardRepository.FirstOrDefaultAsync(r => r.Id == input.RewardId)
                ?? throw new UserFriendlyException("This reward is no longer available.");

            var now = Clock.Now;
            if ((reward.ValidFrom.HasValue && reward.ValidFrom.Value > now) ||
                (reward.ValidTo.HasValue && reward.ValidTo.Value < now))
            {
                throw new UserFriendlyException("This reward isn't currently available.");
            }

            if (reward.StockRemaining.HasValue && reward.StockRemaining.Value <= 0)
            {
                throw new UserFriendlyException("This reward is out of stock.");
            }

            // One open redemption per customer per reward. Without this, tapping Redeem twice (a
            // double-tap, or a return to the screen) silently stacks a second hold on the same wallet
            // and hands staff two codes for one drink.
            var alreadyPending = await _couponRepository.FirstOrDefaultAsync(c =>
                c.MembershipId == membership.Id &&
                c.RewardId == reward.Id &&
                c.Status == CouponStatus.Pending);

            if (alreadyPending != null)
            {
                // Idempotent rather than an error: the customer's intent hasn't changed, so hand back
                // the code they already have instead of making them cancel one to get another.
                return await ToDtoAsync(alreadyPending, reward);
            }

            var wallet = await _walletRepository.FirstAsync(w => w.MembershipId == membership.Id);

            // Reserve() checks AvailableBalance and throws the same UserFriendlyException the old
            // inline balance check did, so the customer-facing message is unchanged.
            wallet.Reserve(reward.PointsCost);
            await _walletRepository.UpdateAsync(wallet);

            var code = await GenerateUniqueCodeAsync();
            var coupon = Coupon.CreatePending(
                GuidGenerator.Create(),
                reward.Id,
                membership.Id,
                code,
                reward.PointsCost,
                now,
                now.AddMinutes(CouponConsts.PendingWindowMinutes));

            await _couponRepository.InsertAsync(coupon);

            // Stock is held alongside the points, for the same reason: two customers must not both be
            // promised the last unit. IncrementStock puts it back on every release path.
            reward.DecrementStock();
            await _rewardRepository.UpdateAsync(reward);

            // No PointsTransaction row here — deliberately. The ledger records movements, and nothing
            // has moved yet; the Redeem row is written on approval. See PointsWallet.Reserved.
            return await ToDtoAsync(coupon, reward);
        }
    }

    public async Task<CouponDto> GetMyCouponAsync(Guid tenantId, Guid couponId)
    {
        using (_currentTenant.Change(tenantId))
        {
            var (coupon, _) = await GetOwnCouponAsync(couponId);
            return await ToDtoAsync(coupon, reward: null);
        }
    }

    public async Task<CouponDto> CancelMyCouponAsync(Guid tenantId, Guid couponId)
    {
        using (_currentTenant.Change(tenantId))
        {
            var (coupon, membership) = await GetOwnCouponAsync(couponId);

            if (!coupon.IsAwaitingApproval)
            {
                throw new UserFriendlyException("This redemption is no longer awaiting approval.");
            }

            coupon.Cancel();
            await _couponRepository.UpdateAsync(coupon);

            var wallet = await _walletRepository.FirstAsync(w => w.MembershipId == membership.Id);
            wallet.ReleaseReservation(coupon.PointsCost);
            await _walletRepository.UpdateAsync(wallet);

            var reward = await _rewardRepository.FirstOrDefaultAsync(r => r.Id == coupon.RewardId);
            if (reward != null)
            {
                reward.IncrementStock();
                await _rewardRepository.UpdateAsync(reward);
            }

            return await ToDtoAsync(coupon, reward);
        }
    }

    // Loads a coupon and proves it belongs to the caller. Ownership is checked through Membership
    // rather than trusting the id — the ambient tenant filter scopes the row to one business, but not
    // to one customer within it.
    private async Task<(Coupon Coupon, Membership Membership)> GetOwnCouponAsync(Guid couponId)
    {
        var customerId = CurrentUser.GetId();

        var membership = await _membershipRepository.FirstOrDefaultAsync(m => m.CustomerId == customerId)
            ?? throw new UserFriendlyException("You haven't joined this business yet.");

        var coupon = await _couponRepository.FirstOrDefaultAsync(c =>
            c.Id == couponId && c.MembershipId == membership.Id)
            ?? throw new EntityNotFoundException(typeof(Coupon), couponId);

        return (coupon, membership);
    }

    private async Task<CouponDto> ToDtoAsync(Coupon coupon, Reward? reward)
    {
        var dto = ObjectMapper.Map<Coupon, CouponDto>(coupon);

        reward ??= await _rewardRepository.FirstOrDefaultAsync(r => r.Id == coupon.RewardId);
        if (reward != null)
        {
            dto.RewardNameAr = reward.NameAr;
            dto.RewardNameEn = reward.NameEn;
        }

        return dto;
    }

    public async Task<List<CouponDto>> GetMyCouponsAsync(Guid? tenantId = null)
    {
        var customerId = CurrentUser.GetId();

        List<Coupon> coupons;
        if (tenantId.HasValue)
        {
            using (_currentTenant.Change(tenantId.Value))
            {
                var membership = await _membershipRepository.FirstOrDefaultAsync(m => m.CustomerId == customerId);
                coupons = membership == null
                    ? new List<Coupon>()
                    : await _couponRepository.GetListAsync(c => c.MembershipId == membership.Id);
            }
        }
        else
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var membershipIds = (await _membershipRepository.GetListAsync(m => m.CustomerId == customerId))
                    .Select(m => m.Id)
                    .ToList();
                coupons = await _couponRepository.GetListAsync(c => membershipIds.Contains(c.MembershipId));
            }
        }

        var dtos = ObjectMapper.Map<List<Coupon>, List<CouponDto>>(coupons);
        await SetRewardNamesAsync(dtos);
        return dtos;
    }

    private async Task<string> GenerateUniqueCodeAsync()
    {
        for (var attempt = 0; attempt < MaxCodeGenerationAttempts; attempt++)
        {
            // Guid.NewGuid() (not GuidGenerator.Create()) — the code needs uniform randomness across
            // its whole length; ABP's sequential ID generator front-loads a time-derived prefix, which
            // would make same-millisecond codes collide far more than a real random source would.
            var code = Guid.NewGuid().ToString("N")[..CouponConsts.CodeLength].ToUpperInvariant();

            // The uniqueness check itself must ignore the IMultiTenant filter — Coupon.Code is
            // globally unique across all tenants (see EksabliDbContext), but the ambient tenant scope
            // here would only see this tenant's rows, letting a cross-tenant collision slip through.
            using (_dataFilter.Disable<IMultiTenant>())
            {
                if (!await _couponRepository.AnyAsync(c => c.Code == code))
                {
                    return code;
                }
            }
        }

        throw new UserFriendlyException("Couldn't generate a redemption code. Please try again.");
    }

    private async Task SetRewardNamesAsync(List<CouponDto> dtos)
    {
        if (dtos.Count == 0)
        {
            return;
        }

        var rewardIds = dtos.Select(d => d.RewardId).Distinct().ToList();
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var rewards = await _rewardRepository.GetListAsync(r => rewardIds.Contains(r.Id));
            var lookup = rewards.ToDictionary(r => r.Id, r => (r.NameAr, r.NameEn));

            foreach (var dto in dtos)
            {
                if (lookup.TryGetValue(dto.RewardId, out var names))
                {
                    dto.RewardNameAr = names.NameAr;
                    dto.RewardNameEn = names.NameEn;
                }
            }
        }
    }
}
