using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Eksabli.Wallets;
using Shouldly;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Wallets;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class PointsExpirationWorker_Tests : EksabliEntityFrameworkCoreTestBase
{
    private readonly PointsExpirationWorker _worker;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<PointsTransaction, Guid> _transactionRepository;
    private readonly ICurrentTenant _currentTenant;

    public PointsExpirationWorker_Tests()
    {
        _worker = GetRequiredService<PointsExpirationWorker>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _walletRepository = GetRequiredService<IRepository<PointsWallet, Guid>>();
        _transactionRepository = GetRequiredService<IRepository<PointsTransaction, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private async Task RunWorkerOnceAsync()
    {
        var method = typeof(PointsExpirationWorker).GetMethod("DoWorkAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var context = new PeriodicBackgroundWorkerContext(ServiceProvider);
        await (Task)method.Invoke(_worker, new object[] { context })!;
    }

    [Fact]
    public async Task Should_Expire_Overdue_Transactions_Across_Tenants_And_Be_Idempotent()
    {
        Guid tenantId = default, walletId = default, sourceTransactionId = default;

        await WithUnitOfWorkAsync(async () =>
        {
            var tenant = await _tenantManager.CreateAsync("tenant-" + Guid.NewGuid().ToString("N"));
            await _tenantRepository.InsertAsync(tenant, autoSave: true);
            tenantId = tenant.Id;
        });

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var wallet = PointsWallet.Create(Guid.NewGuid(), Guid.NewGuid());
                wallet.ApplyTransaction(PointsTransactionType.Earn, 100);
                await _walletRepository.InsertAsync(wallet, autoSave: true);
                walletId = wallet.Id;

                var overdue = PointsTransaction.Create(
                    Guid.NewGuid(), wallet.Id, PointsTransactionType.Earn, 100, PointsTransactionSource.Purchase,
                    expiresAt: DateTime.UtcNow.AddDays(-1));
                await _transactionRepository.InsertAsync(overdue, autoSave: true);
                sourceTransactionId = overdue.Id;
            }
        });

        await RunWorkerOnceAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var expireRows = await _transactionRepository.GetListAsync(t => t.Type == PointsTransactionType.Expire);
                expireRows.Count.ShouldBe(1);
                expireRows.Single().ReferenceId.ShouldBe(sourceTransactionId);
                expireRows.Single().Points.ShouldBe(-100);

                var wallet = await _walletRepository.GetAsync(walletId);
                wallet.Balance.ShouldBe(0); // 100 earned, then expired
            }
        });

        // Running the sweep again must not double-expire the same source transaction.
        await RunWorkerOnceAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var expireRows = await _transactionRepository.GetListAsync(t => t.Type == PointsTransactionType.Expire);
                expireRows.Count.ShouldBe(1);
            }
        });
    }
}
