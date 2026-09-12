using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Eksabli.Wallets;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.Memberships;

public abstract class MembershipAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IMembershipAppService _membershipAppService;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<Tier, Guid> _tierRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    protected MembershipAppService_Tests()
    {
        _membershipAppService = GetRequiredService<IMembershipAppService>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _membershipRepository = GetRequiredService<IRepository<Membership, Guid>>();
        _walletRepository = GetRequiredService<IRepository<PointsWallet, Guid>>();
        _tierRepository = GetRequiredService<IRepository<Tier, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private IDisposable LoginAs(Guid userId)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(AbpClaimTypes.UserId, userId.ToString()));
        return _currentPrincipalAccessor.Change(new ClaimsPrincipal(identity));
    }

    private async Task<Guid> CreateTenantAsync()
    {
        Guid tenantId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            var tenant = await _tenantManager.CreateAsync("tenant-" + Guid.NewGuid().ToString("N"));
            await _tenantRepository.InsertAsync(tenant, autoSave: true);
            tenantId = tenant.Id;
        });
        return tenantId;
    }

    [Fact]
    public async Task Should_Join_Creates_Membership_And_Wallet_Atomically()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();

        using (LoginAs(customerId))
        {
            var membership = await WithUnitOfWorkAsync(() => _membershipAppService.JoinAsync(new JoinBusinessDto { TenantId = tenantId }));
            membership.CustomerId.ShouldBe(customerId);
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var membership = await _membershipRepository.SingleAsync(m => m.CustomerId == customerId);
                var wallet = await _walletRepository.FirstOrDefaultAsync(w => w.MembershipId == membership.Id);
                wallet.ShouldNotBeNull();
                wallet!.Balance.ShouldBe(0);
            }
        });
    }

    [Fact]
    public async Task Should_Not_Join_The_Same_Business_Twice()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();

        using (LoginAs(customerId))
        {
            await WithUnitOfWorkAsync(() => _membershipAppService.JoinAsync(new JoinBusinessDto { TenantId = tenantId }));

            await Assert.ThrowsAsync<UserFriendlyException>(async () =>
            {
                await WithUnitOfWorkAsync(() => _membershipAppService.JoinAsync(new JoinBusinessDto { TenantId = tenantId }));
            });
        }
    }

    [Fact]
    public async Task Should_Aggregate_Memberships_And_Wallets_Across_Two_Tenants()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var customerId = Guid.NewGuid();

        using (LoginAs(customerId))
        {
            await WithUnitOfWorkAsync(() => _membershipAppService.JoinAsync(new JoinBusinessDto { TenantId = tenantA }));
            await WithUnitOfWorkAsync(() => _membershipAppService.JoinAsync(new JoinBusinessDto { TenantId = tenantB }));

            var memberships = await WithUnitOfWorkAsync(() => _membershipAppService.GetMyMembershipsAsync());
            memberships.Count.ShouldBe(2);
            memberships.Select(m => m.TenantId).ShouldBe(new Guid?[] { tenantA, tenantB }, ignoreOrder: true);

            var wallets = await WithUnitOfWorkAsync(() => _membershipAppService.GetMyWalletsAsync());
            wallets.Count.ShouldBe(2);
        }
    }

    private async Task SeedTiersAsync(Guid tenantId)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                foreach (var (name, floor) in new[] { ("Bronze", 0), ("Silver", 500), ("Gold", 2000), ("Platinum", 5000) })
                {
                    await _tierRepository.InsertAsync(Tier.Create(Guid.NewGuid(), name, floor, 1m), autoSave: true);
                }
            }
        });
    }

    private async Task SetLifetimeEarnedAsync(Guid tenantId, Guid customerId, int lifetimeEarned)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var membership = await _membershipRepository.SingleAsync(m => m.CustomerId == customerId);
                var wallet = await _walletRepository.SingleAsync(w => w.MembershipId == membership.Id);
                wallet.ApplyTransaction(PointsTransactionType.Earn, lifetimeEarned);
                await _walletRepository.UpdateAsync(wallet, autoSave: true);
            }
        });
    }

    [Fact]
    public async Task Should_Report_The_Tier_The_Customer_Qualifies_For_And_The_Next_One()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await SeedTiersAsync(tenantId);

        using (LoginAs(customerId))
        {
            await WithUnitOfWorkAsync(() => _membershipAppService.JoinAsync(new JoinBusinessDto { TenantId = tenantId }));
            await SetLifetimeEarnedAsync(tenantId, customerId, 3400);

            var wallet = (await WithUnitOfWorkAsync(() => _membershipAppService.GetMyWalletsAsync())).Single();

            wallet.CurrentTierName.ShouldBe("Gold");
            wallet.CurrentTierMinLifetimePoints.ShouldBe(2000);
            wallet.NextTierName.ShouldBe("Platinum");
            wallet.NextTierMinLifetimePoints.ShouldBe(5000);
        }
    }

    [Fact]
    public async Task Should_Report_A_Tier_Even_When_The_Cached_TierId_Was_Never_Set()
    {
        // Wallets whose balance did not arrive through PosAppService — a seed, a migration, a manual
        // correction — never ran TierRecomputeService, so CurrentTierId is null while the customer
        // plainly qualifies. Showing them no tier at all is worse than applying the same rule.
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await SeedTiersAsync(tenantId);

        using (LoginAs(customerId))
        {
            await WithUnitOfWorkAsync(() => _membershipAppService.JoinAsync(new JoinBusinessDto { TenantId = tenantId }));
            await SetLifetimeEarnedAsync(tenantId, customerId, 5400);

            var wallet = (await WithUnitOfWorkAsync(() => _membershipAppService.GetMyWalletsAsync())).Single();

            wallet.CurrentTierId.ShouldBeNull();          // the cache really is empty...
            wallet.CurrentTierName.ShouldBe("Platinum");  // ...and the answer is still right
        }
    }

    [Fact]
    public async Task Should_Report_No_Next_Tier_On_The_Top_Rung()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await SeedTiersAsync(tenantId);

        using (LoginAs(customerId))
        {
            await WithUnitOfWorkAsync(() => _membershipAppService.JoinAsync(new JoinBusinessDto { TenantId = tenantId }));
            await SetLifetimeEarnedAsync(tenantId, customerId, 9000);

            var wallet = (await WithUnitOfWorkAsync(() => _membershipAppService.GetMyWalletsAsync())).Single();

            wallet.CurrentTierName.ShouldBe("Platinum");
            // The UI keys off this to hide the progress bar rather than draw one with no target.
            wallet.NextTierName.ShouldBeNull();
            wallet.NextTierMinLifetimePoints.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Should_Leave_Tier_Fields_Empty_When_The_Business_Defines_No_Tiers()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();

        using (LoginAs(customerId))
        {
            await WithUnitOfWorkAsync(() => _membershipAppService.JoinAsync(new JoinBusinessDto { TenantId = tenantId }));
            await SetLifetimeEarnedAsync(tenantId, customerId, 3400);

            var wallet = (await WithUnitOfWorkAsync(() => _membershipAppService.GetMyWalletsAsync())).Single();

            wallet.CurrentTierName.ShouldBeNull();
            wallet.NextTierName.ShouldBeNull();
        }
    }
}
