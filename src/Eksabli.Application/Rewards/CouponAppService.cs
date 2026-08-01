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
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace Eksabli.Rewards;

public class CouponAppService : ApplicationService, ICouponAppService
{
    private const int MaxCodeGenerationAttempts = 5;

    private readonly IRewardRepository _rewardRepository;
    private readonly ICouponRepository _couponRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<PointsTransaction, Guid> _transactionRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    public CouponAppService(
        IRewardRepository rewardRepository,
        ICouponRepository couponRepository,
        IRepository<Membership, Guid> membershipRepository,
        IRepository<PointsWallet, Guid> walletRepository,
        IRepository<PointsTransaction, Guid> transactionRepository,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter)
    {
        _rewardRepository = rewardRepository;
        _couponRepository = couponRepository;
        _membershipRepository = membershipRepository;
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
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

            var wallet = await _walletRepository.FirstAsync(w => w.MembershipId == membership.Id);
            if (wallet.Balance < reward.PointsCost)
            {
                throw new UserFriendlyException("You don't have enough points to redeem this reward.");
            }

            var code = await GenerateUniqueCodeAsync();
            var coupon = Coupon.Create(GuidGenerator.Create(), reward.Id, membership.Id, code, now);
            await _couponRepository.InsertAsync(coupon);

            var transaction = PointsTransaction.Create(
                GuidGenerator.Create(),
                wallet.Id,
                PointsTransactionType.Redeem,
                -reward.PointsCost,
                PointsTransactionSource.Reward,
                referenceId: coupon.Id);
            await _transactionRepository.InsertAsync(transaction);

            wallet.ApplyTransaction(PointsTransactionType.Redeem, -reward.PointsCost);
            await _walletRepository.UpdateAsync(wallet);

            reward.DecrementStock();
            await _rewardRepository.UpdateAsync(reward);

            var dto = ObjectMapper.Map<Coupon, CouponDto>(coupon);
            dto.RewardNameAr = reward.NameAr;
            dto.RewardNameEn = reward.NameEn;
            return dto;
        }
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
