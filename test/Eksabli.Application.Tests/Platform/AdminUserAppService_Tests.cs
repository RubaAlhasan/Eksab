using System;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.CustomerProfiles;
using Eksabli.Memberships;
using Eksabli.Wallets;
using Microsoft.AspNetCore.Identity;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.Platform;

public abstract class AdminUserAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAdminUserAppService _adminUserAppService;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IdentityUserManager _identityUserManager;
    private readonly IRepository<CustomerProfile, Guid> _customerProfileRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<Wallets.Tier, Guid> _tierRepository;
    private readonly IRepository<PointsTransaction, Guid> _transactionRepository;
    private readonly ICurrentTenant _currentTenant;

    protected AdminUserAppService_Tests()
    {
        _adminUserAppService = GetRequiredService<IAdminUserAppService>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _identityUserManager = GetRequiredService<IdentityUserManager>();
        _customerProfileRepository = GetRequiredService<IRepository<CustomerProfile, Guid>>();
        _membershipRepository = GetRequiredService<IRepository<Membership, Guid>>();
        _walletRepository = GetRequiredService<IRepository<PointsWallet, Guid>>();
        _tierRepository = GetRequiredService<IRepository<Wallets.Tier, Guid>>();
        _transactionRepository = GetRequiredService<IRepository<PointsTransaction, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private async Task<Guid> CreateTenantAsync(string name)
    {
        Guid tenantId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            var tenant = await _tenantManager.CreateAsync(name);
            await _tenantRepository.InsertAsync(tenant, autoSave: true);
            tenantId = tenant.Id;
        });
        return tenantId;
    }

    private async Task<Guid> CreateCustomerAsync()
    {
        Guid customerId = default;
        var phoneNumber = "+1555" + Random.Shared.Next(1000000, 9999999);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                var user = new IdentityUser(Guid.NewGuid(), phoneNumber, $"{Guid.NewGuid():N}@otp.eksabli.local", tenantId: null);
                (await _identityUserManager.CreateAsync(user)).CheckErrors();
                await _identityUserManager.SetPhoneNumberAsync(user, phoneNumber);
                customerId = user.Id;

                var profile = CustomerProfile.Create(Guid.NewGuid(), customerId);
                profile.SetName("Jane", "Doe");
                await _customerProfileRepository.InsertAsync(profile, autoSave: true);
            }
        });

        return customerId;
    }

    // Mirrors PosAppService_Tests.JoinBusinessAsync, plus an optional tier and a couple of ledger
    // entries applied directly (not through IPosAppService) — this test suite is exercising
    // AdminUserAppService's own cross-tenant read, not the award pipeline.
    private async Task<(Guid MembershipId, Guid WalletId)> JoinBusinessAsync(Guid tenantId, Guid customerId, Guid? tierId = null, int earnPoints = 0)
    {
        Guid membershipId = default;
        Guid walletId = default;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var membership = Membership.Create(Guid.NewGuid(), customerId, DateTime.UtcNow);
                await _membershipRepository.InsertAsync(membership, autoSave: true);
                membershipId = membership.Id;

                var wallet = PointsWallet.Create(Guid.NewGuid(), membership.Id);
                if (tierId.HasValue) wallet.ChangeTier(tierId.Value);
                await _walletRepository.InsertAsync(wallet, autoSave: true);
                walletId = wallet.Id;

                if (earnPoints > 0)
                {
                    var tx = PointsTransaction.Create(Guid.NewGuid(), wallet.Id, PointsTransactionType.Earn, earnPoints, PointsTransactionSource.Purchase);
                    await _transactionRepository.InsertAsync(tx, autoSave: true);
                    wallet.ApplyTransaction(PointsTransactionType.Earn, earnPoints);
                    await _walletRepository.UpdateAsync(wallet, autoSave: true);
                }
            }
        });

        return (membershipId, walletId);
    }

    private async Task<Guid> CreateTierAsync(Guid tenantId, string name, decimal multiplier)
    {
        Guid tierId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var tier = Wallets.Tier.Create(Guid.NewGuid(), name, 0, multiplier);
                await _tierRepository.InsertAsync(tier, autoSave: true);
                tierId = tier.Id;
            }
        });
        return tierId;
    }

    [Fact]
    public async Task Should_List_Every_Business_Membership_With_Its_Own_Independent_Wallet()
    {
        var tenantA = await CreateTenantAsync("cafe-" + Guid.NewGuid().ToString("N")[..8]);
        var tenantB = await CreateTenantAsync("gym-" + Guid.NewGuid().ToString("N")[..8]);
        var customerId = await CreateCustomerAsync();

        var goldTier = await CreateTierAsync(tenantA, "Gold", 1.5m);
        await JoinBusinessAsync(tenantA, customerId, tierId: goldTier, earnPoints: 450);
        await JoinBusinessAsync(tenantB, customerId, earnPoints: 120);

        var detail = await WithUnitOfWorkAsync(() => _adminUserAppService.GetCustomerDetailAsync(customerId));

        detail.FirstName.ShouldBe("Jane");
        detail.LastName.ShouldBe("Doe");
        detail.Memberships.Count.ShouldBe(2);

        var membershipA = detail.Memberships.Single(m => m.TenantId == tenantA);
        membershipA.Balance.ShouldBe(450);
        membershipA.LifetimeEarned.ShouldBe(450);
        membershipA.TierName.ShouldBe("Gold");

        var membershipB = detail.Memberships.Single(m => m.TenantId == tenantB);
        membershipB.Balance.ShouldBe(120);
        membershipB.TierName.ShouldBeNull();

        // The two businesses' balances are independent — never summed, never conflated (see
        // AdminCustomerMembershipDto's own comment on why: they're different currencies).
        membershipA.Balance.ShouldNotBe(membershipB.Balance);
    }

    [Fact]
    public async Task Should_Scope_Transaction_History_To_The_Membership_Own_Tenant()
    {
        var tenantA = await CreateTenantAsync("cafe-" + Guid.NewGuid().ToString("N")[..8]);
        var tenantB = await CreateTenantAsync("gym-" + Guid.NewGuid().ToString("N")[..8]);
        var customerId = await CreateCustomerAsync();

        var (membershipA, _) = await JoinBusinessAsync(tenantA, customerId, earnPoints: 30);
        await JoinBusinessAsync(tenantB, customerId, earnPoints: 999); // a very different number, on purpose

        var page = await WithUnitOfWorkAsync(() => _adminUserAppService.GetCustomerTransactionsAsync(
            membershipA, tenantA, new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto()));

        page.TotalCount.ShouldBe(1);
        page.Items.Single().Points.ShouldBe(30);
        page.Items.ShouldAllBe(t => t.Points != 999);
    }

    [Fact]
    public async Task Should_Throw_For_An_Unknown_Customer_Id()
    {
        // GetCustomerDetailAsync's IIdentityUserRepository.GetAsync(id) throws ABP's generic
        // EntityNotFoundException<IdentityUser> (a subtype of the non-generic EntityNotFoundException) —
        // xUnit's Assert.ThrowsAsync<T> needs the exact runtime type, not just an assignable base.
        await Assert.ThrowsAsync<EntityNotFoundException<Volo.Abp.Identity.IdentityUser>>(async () =>
        {
            await WithUnitOfWorkAsync(() => _adminUserAppService.GetCustomerDetailAsync(Guid.NewGuid()));
        });
    }
}
