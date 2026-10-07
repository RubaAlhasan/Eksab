using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace Eksabli.Wallets;

// First AsyncPeriodicBackgroundWorkerBase in this repo. Daily sweep that expires the points of every award whose
// ExpiresAt has passed, as an Expire ledger row — expiration is itself a ledger entry, never a silent balance edit
// (per docs/eksabli-loyalty-platform/07-loyalty-engine.md#8-points-system).
//
// Only the points an award still holds are expired. A customer who already spent part of an award keeps nothing of it
// past its expiry, and never drops below the points held for a pending reward (see ExpireOverdueTransactionsAsync).
public class PointsExpirationWorker : AsyncPeriodicBackgroundWorkerBase
{
    public PointsExpirationWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = 24 * 60 * 60 * 1000; // daily
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var tenantRepository = workerContext.ServiceProvider.GetRequiredService<ITenantRepository>();
        var currentTenant = workerContext.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var unitOfWorkManager = workerContext.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var transactionRepository = workerContext.ServiceProvider.GetRequiredService<IRepository<PointsTransaction, Guid>>();
        var walletRepository = workerContext.ServiceProvider.GetRequiredService<IRepository<PointsWallet, Guid>>();
        var guidGenerator = workerContext.ServiceProvider.GetRequiredService<Volo.Abp.Guids.IGuidGenerator>();
        var clock = workerContext.ServiceProvider.GetRequiredService<IClock>();

        var tenants = await tenantRepository.GetListAsync();

        foreach (var tenant in tenants)
        {
            using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            using (currentTenant.Change(tenant.Id))
            {
                await ExpireOverdueTransactionsAsync(transactionRepository, walletRepository, guidGenerator, clock);
            }
            await uow.CompleteAsync();
        }
    }

    private static async Task ExpireOverdueTransactionsAsync(
        IRepository<PointsTransaction, Guid> transactionRepository,
        IRepository<PointsWallet, Guid> walletRepository,
        Volo.Abp.Guids.IGuidGenerator guidGenerator,
        IClock clock)
    {
        var now = clock.Now;

        // Only wallets holding an award past its ExpiresAt are replayed. Awards already fully spent or expired are
        // still listed here, so this runs against every such wallet each day; the replay makes that harmless.
        var walletIds = (await transactionRepository.GetListAsync(t =>
                t.ExpiresAt != null && t.ExpiresAt <= now && t.Points > 0))
            .Select(t => t.WalletId)
            .Distinct()
            .ToList();

        foreach (var walletId in walletIds)
        {
            var wallet = await walletRepository.GetAsync(walletId);
            var ledger = await transactionRepository.GetListAsync(t => t.WalletId == walletId);

            // Points held against a pending redemption are not spendable, so expiry may take at most what is available.
            // The reserved points stay on the ledger and are settled by the redemption itself.
            var budget = wallet.AvailableBalance;
            var overdueLots = PointsLotReplay.Replay(ledger)
                .Where(lot => lot.ExpiresAt is { } expiresAt && expiresAt <= now && lot.Remaining > 0)
                .OrderBy(lot => lot.CreationTime);

            var expiredAny = false;
            foreach (var lot in overdueLots)
            {
                if (budget <= 0) break;

                var expired = Math.Min(lot.Remaining, budget);
                budget -= expired;

                var expireTransaction = PointsTransaction.Create(
                    guidGenerator.Create(),
                    walletId,
                    PointsTransactionType.Expire,
                    -expired,
                    lot.Source,
                    referenceId: lot.Id);

                await transactionRepository.InsertAsync(expireTransaction);
                wallet.ApplyTransaction(PointsTransactionType.Expire, -expired);
                expiredAny = true;
            }

            if (expiredAny)
            {
                await walletRepository.UpdateAsync(wallet);
            }
        }
    }
}
