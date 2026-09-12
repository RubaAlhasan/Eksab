using System;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.Wallets;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace Eksabli.Rewards;

// Fourth AsyncPeriodicBackgroundWorkerBase in this repo (mirrors Wallets.PointsExpirationWorker,
// Billing.SubscriptionRenewalWorker, Campaigns.CampaignSweepWorker).
//
// This one is the safety net that makes the reservation flow honest: every Pending coupon holds points
// the customer cannot spend, so something has to release the hold when nobody ever approves or rejects
// it — the customer changed their mind, the queue moved on, the tablet died mid-shift.
//
// Runs every FIVE MINUTES, not daily like its three siblings. Their subjects (points expiry, renewals,
// campaign sweeps) are day-grained by nature; this one gates spendable balance. A customer who
// abandons a redemption and tries again two minutes later must not be told they are short on points
// because yesterday's sweep hasn't run. The window itself is CouponConsts.PendingWindowMinutes (15), so
// a five-minute tick returns the points within 20 minutes worst case.
public class RedemptionReservationWorker : AsyncPeriodicBackgroundWorkerBase
{
    public RedemptionReservationWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = 5 * 60 * 1000; // every 5 minutes
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var tenantRepository = workerContext.ServiceProvider.GetRequiredService<ITenantRepository>();
        var currentTenant = workerContext.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var unitOfWorkManager = workerContext.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        var tenants = await tenantRepository.GetListAsync();

        foreach (var tenant in tenants)
        {
            using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            using (currentTenant.Change(tenant.Id))
            {
                await ReleaseLapsedReservationsAsync(workerContext.ServiceProvider);
            }
            await uow.CompleteAsync();
        }
    }

    private static async Task ReleaseLapsedReservationsAsync(IServiceProvider serviceProvider)
    {
        var couponRepository = serviceProvider.GetRequiredService<ICouponRepository>();
        var walletRepository = serviceProvider.GetRequiredService<IRepository<PointsWallet, Guid>>();
        var rewardRepository = serviceProvider.GetRequiredService<IRewardRepository>();
        var clock = serviceProvider.GetRequiredService<IClock>();

        var now = clock.Now;

        var lapsed = await couponRepository.GetListAsync(c =>
            c.Status == CouponStatus.Pending &&
            c.ReservationExpiresAt != null &&
            c.ReservationExpiresAt <= now);

        if (lapsed.Count == 0)
        {
            return;
        }

        // One wallet can hold several lapsed reservations at once (a customer who queued up two
        // rewards and abandoned both), so releases are grouped per wallet and the entity is saved once
        // with the total — not re-fetched and re-saved per coupon.
        var membershipIds = lapsed.Select(c => c.MembershipId).Distinct().ToList();
        var wallets = (await walletRepository.GetListAsync(w => membershipIds.Contains(w.MembershipId)))
            .ToDictionary(w => w.MembershipId);

        var touchedWallets = new System.Collections.Generic.HashSet<Guid>();

        foreach (var coupon in lapsed)
        {
            coupon.MarkExpired();
            await couponRepository.UpdateAsync(coupon);

            if (wallets.TryGetValue(coupon.MembershipId, out var wallet))
            {
                wallet.ReleaseReservation(coupon.PointsCost);
                touchedWallets.Add(wallet.Id);
            }

            // Stock was decremented when the reservation was taken, so an abandoned redemption has to
            // put the unit back or the reward silently sells out to nobody.
            var reward = await rewardRepository.FirstOrDefaultAsync(r => r.Id == coupon.RewardId);
            if (reward != null)
            {
                reward.IncrementStock();
                await rewardRepository.UpdateAsync(reward);
            }
        }

        foreach (var wallet in wallets.Values.Where(w => touchedWallets.Contains(w.Id)))
        {
            await walletRepository.UpdateAsync(wallet);
        }
    }
}
