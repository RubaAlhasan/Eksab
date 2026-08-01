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

// First AsyncPeriodicBackgroundWorkerBase in this repo. Daily sweep that turns any PointsTransaction
// past its ExpiresAt into a matching Expire ledger row — expiration is itself a ledger entry, never
// a silent balance edit (per docs/eksabli-loyalty-platform/07-loyalty-engine.md#8-points-system).
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

        var candidates = await transactionRepository.GetListAsync(t =>
            t.ExpiresAt != null && t.ExpiresAt <= now && t.Type != PointsTransactionType.Expire);

        if (candidates.Count == 0)
        {
            return;
        }

        // Ledger rows are immutable — "already processed" can't be flagged on the original row, so
        // idempotency is a lookup against already-emitted Expire rows' ReferenceId.
        var alreadyExpiredSourceIds = (await transactionRepository.GetListAsync(t => t.Type == PointsTransactionType.Expire))
            .Where(t => t.ReferenceId.HasValue)
            .Select(t => t.ReferenceId!.Value)
            .ToHashSet();

        foreach (var source in candidates.Where(t => !alreadyExpiredSourceIds.Contains(t.Id)))
        {
            var wallet = await walletRepository.GetAsync(source.WalletId);

            var expireTransaction = PointsTransaction.Create(
                guidGenerator.Create(),
                source.WalletId,
                PointsTransactionType.Expire,
                -source.Points,
                source.Source,
                referenceId: source.Id);

            await transactionRepository.InsertAsync(expireTransaction);

            wallet.ApplyTransaction(PointsTransactionType.Expire, -source.Points);
            await walletRepository.UpdateAsync(wallet);
        }
    }
}
