using System;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.Wallets;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Wallets;

// Direct-repository style, mirrors MembershipMultiTenancy_Tests — proves the ledger invariant
// (Balance == SUM(Points)) holds across a mixed sequence of transaction types, independent of any
// app service.
[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class PointsWalletLedgerInvariant_Tests : EksabliEntityFrameworkCoreTestBase
{
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<PointsTransaction, Guid> _transactionRepository;

    public PointsWalletLedgerInvariant_Tests()
    {
        _walletRepository = GetRequiredService<IRepository<PointsWallet, Guid>>();
        _transactionRepository = GetRequiredService<IRepository<PointsTransaction, Guid>>();
    }

    [Fact]
    public async Task Balance_Should_Always_Equal_The_Sum_Of_Ledger_Points()
    {
        var wallet = PointsWallet.Create(Guid.NewGuid(), Guid.NewGuid());
        await WithUnitOfWorkAsync(() => _walletRepository.InsertAsync(wallet, autoSave: true));

        var sequence = new[]
        {
            (PointsTransactionType.Earn, 100),
            (PointsTransactionType.Redeem, -30),
            (PointsTransactionType.Adjust, 10),
            (PointsTransactionType.Adjust, -5),
            (PointsTransactionType.Refund, 30),
            (PointsTransactionType.Expire, -20)
        };

        foreach (var (type, points) in sequence)
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var w = await _walletRepository.GetAsync(wallet.Id);
                var tx = PointsTransaction.Create(Guid.NewGuid(), w.Id, type, points, PointsTransactionSource.Manual);
                await _transactionRepository.InsertAsync(tx, autoSave: true);

                w.ApplyTransaction(type, points);
                await _walletRepository.UpdateAsync(w, autoSave: true);
            });
        }

        await WithUnitOfWorkAsync(async () =>
        {
            var finalWallet = await _walletRepository.GetAsync(wallet.Id);
            var transactions = await _transactionRepository.GetListAsync(t => t.WalletId == wallet.Id);

            finalWallet.Balance.ShouldBe(transactions.Sum(t => t.Points));
            finalWallet.Balance.ShouldBe(100 - 30 + 10 - 5 + 30 - 20);
        });
    }

    [Fact]
    public async Task Adjust_And_Expire_Should_Never_Move_Lifetime_Counters()
    {
        var wallet = PointsWallet.Create(Guid.NewGuid(), Guid.NewGuid());
        await WithUnitOfWorkAsync(() => _walletRepository.InsertAsync(wallet, autoSave: true));

        await WithUnitOfWorkAsync(async () =>
        {
            var w = await _walletRepository.GetAsync(wallet.Id);
            w.ApplyTransaction(PointsTransactionType.Earn, 100);
            await _walletRepository.UpdateAsync(w, autoSave: true);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var w = await _walletRepository.GetAsync(wallet.Id);
            w.ApplyTransaction(PointsTransactionType.Adjust, 500); // large positive adjustment
            w.ApplyTransaction(PointsTransactionType.Expire, -10);
            await _walletRepository.UpdateAsync(w, autoSave: true);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var finalWallet = await _walletRepository.GetAsync(wallet.Id);

            // A manual adjustment must never be usable to inflate LifetimeEarned (tier-qualification
            // driver) or LifetimeRedeemed — only real Earn/Redeem/Refund transactions move those.
            finalWallet.LifetimeEarned.ShouldBe(100);
            finalWallet.LifetimeRedeemed.ShouldBe(0);
            finalWallet.Balance.ShouldBe(100 + 500 - 10);
        });
    }

    [Fact]
    public async Task Redeem_Should_Move_LifetimeRedeemed_By_The_Positive_Amount()
    {
        var wallet = PointsWallet.Create(Guid.NewGuid(), Guid.NewGuid());
        await WithUnitOfWorkAsync(() => _walletRepository.InsertAsync(wallet, autoSave: true));

        await WithUnitOfWorkAsync(async () =>
        {
            var w = await _walletRepository.GetAsync(wallet.Id);
            w.ApplyTransaction(PointsTransactionType.Earn, 100);
            await _walletRepository.UpdateAsync(w, autoSave: true);
        });

        // A reward redemption records a negative Points delta (see PointsTransactionSource.Reward).
        await WithUnitOfWorkAsync(async () =>
        {
            var w = await _walletRepository.GetAsync(wallet.Id);
            var tx = PointsTransaction.Create(Guid.NewGuid(), w.Id, PointsTransactionType.Redeem, -30, PointsTransactionSource.Reward);
            await _transactionRepository.InsertAsync(tx, autoSave: true);

            w.ApplyTransaction(PointsTransactionType.Redeem, -30);
            await _walletRepository.UpdateAsync(w, autoSave: true);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var finalWallet = await _walletRepository.GetAsync(wallet.Id);
            finalWallet.Balance.ShouldBe(70);
            finalWallet.LifetimeEarned.ShouldBe(100);
            finalWallet.LifetimeRedeemed.ShouldBe(30);
        });
    }
}
