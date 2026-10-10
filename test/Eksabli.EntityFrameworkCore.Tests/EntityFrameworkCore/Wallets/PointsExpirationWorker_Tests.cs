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

    [Fact]
    public async Task Should_Expire_Only_The_Unspent_Part_Of_An_Award()
    {
        var tenantId = await CreateTenantAsync();
        var walletId = Guid.Empty;
        var awardId = Guid.Empty;

        // 100 earned on an award that has since expired, then 80 of it spent on a reward.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var wallet = PointsWallet.Create(Guid.NewGuid(), Guid.NewGuid());
                wallet.ApplyTransaction(PointsTransactionType.Earn, 100);
                await _walletRepository.InsertAsync(wallet, autoSave: true);
                walletId = wallet.Id;

                var award = PointsTransaction.Create(
                    Guid.NewGuid(), wallet.Id, PointsTransactionType.Earn, 100, PointsTransactionSource.Purchase,
                    expiresAt: DateTime.UtcNow.AddDays(-1));
                await _transactionRepository.InsertAsync(award, autoSave: true);
                awardId = award.Id;
            }
        });

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var wallet = await _walletRepository.GetAsync(walletId);
                var redeem = PointsTransaction.Create(Guid.NewGuid(), walletId, PointsTransactionType.Redeem, -80, PointsTransactionSource.Reward);
                await _transactionRepository.InsertAsync(redeem, autoSave: true);
                wallet.ApplyTransaction(PointsTransactionType.Redeem, -80);
                await _walletRepository.UpdateAsync(wallet, autoSave: true);
            }
        });

        await RunWorkerOnceAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var expireRows = await _transactionRepository.GetListAsync(t => t.Type == PointsTransactionType.Expire);
                expireRows.Single().ReferenceId.ShouldBe(awardId);
                expireRows.Single().Points.ShouldBe(-20); // only the 20 still held, not the full 100

                var wallet = await _walletRepository.GetAsync(walletId);
                wallet.Balance.ShouldBe(0);
            }
        });
    }

    [Fact]
    public async Task Should_Not_Expire_Points_Held_For_A_Pending_Redemption()
    {
        var tenantId = await CreateTenantAsync();
        var walletId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var wallet = PointsWallet.Create(Guid.NewGuid(), Guid.NewGuid());
                wallet.ApplyTransaction(PointsTransactionType.Earn, 100);
                wallet.Reserve(80); // a pending reward is holding 80 of the 100
                await _walletRepository.InsertAsync(wallet, autoSave: true);
                walletId = wallet.Id;

                var award = PointsTransaction.Create(
                    Guid.NewGuid(), wallet.Id, PointsTransactionType.Earn, 100, PointsTransactionSource.Purchase,
                    expiresAt: DateTime.UtcNow.AddDays(-1));
                await _transactionRepository.InsertAsync(award, autoSave: true);
            }
        });

        await RunWorkerOnceAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var expireRows = await _transactionRepository.GetListAsync(t => t.Type == PointsTransactionType.Expire);
                expireRows.Single().Points.ShouldBe(-20); // the 20 that were free, never the 80 held

                var wallet = await _walletRepository.GetAsync(walletId);
                wallet.Balance.ShouldBe(80);
                wallet.Reserved.ShouldBe(80);
            }
        });
    }

    [Fact]
    public async Task Should_Not_Expire_Awards_Earned_Without_An_Expiry()
    {
        var tenantId = await CreateTenantAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var wallet = PointsWallet.Create(Guid.NewGuid(), Guid.NewGuid());
                wallet.ApplyTransaction(PointsTransactionType.Earn, 100);
                await _walletRepository.InsertAsync(wallet, autoSave: true);

                // The business never expires points, so the award carries no ExpiresAt.
                var award = PointsTransaction.Create(
                    Guid.NewGuid(), wallet.Id, PointsTransactionType.Earn, 100, PointsTransactionSource.Purchase);
                await _transactionRepository.InsertAsync(award, autoSave: true);
            }
        });

        await RunWorkerOnceAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var expireRows = await _transactionRepository.GetListAsync(t => t.Type == PointsTransactionType.Expire);
                expireRows.ShouldBeEmpty();
            }
        });
    }

    private async Task<Guid> CreateTenantAsync()
    {
        var tenantId = Guid.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            var tenant = await _tenantManager.CreateAsync("tenant-" + Guid.NewGuid().ToString("N"));
            await _tenantRepository.InsertAsync(tenant, autoSave: true);
            tenantId = tenant.Id;
        });
        return tenantId;
    }
}
