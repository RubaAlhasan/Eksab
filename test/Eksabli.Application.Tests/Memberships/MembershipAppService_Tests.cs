using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Eksabli.Engagement;
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
    private readonly IReferralAppService _referralAppService;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IReferralRepository _referralRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    protected MembershipAppService_Tests()
    {
        _membershipAppService = GetRequiredService<IMembershipAppService>();
        _referralAppService = GetRequiredService<IReferralAppService>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _membershipRepository = GetRequiredService<IRepository<Membership, Guid>>();
        _walletRepository = GetRequiredService<IRepository<PointsWallet, Guid>>();
        _referralRepository = GetRequiredService<IReferralRepository>();
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

    // Referral join flow, now keyed by Membership.ReferralCode (a short human-typeable code) instead
    // of the referrer's raw Membership.Id — see that property's own comment.
    [Fact]
    public async Task JoinAsync_Should_Create_A_Referral_When_A_Valid_Code_Is_Given()
    {
        var tenantId = await CreateTenantAsync();
        var referrerId = Guid.NewGuid();
        var refereeId = Guid.NewGuid();

        Guid referrerMembershipId = default;
        string referralCode;
        using (LoginAs(referrerId))
        {
            var referrerMembership = await WithUnitOfWorkAsync(() => _membershipAppService.JoinAsync(new JoinBusinessDto { TenantId = tenantId }));
            referrerMembershipId = referrerMembership.Id;
            referralCode = (await WithUnitOfWorkAsync(() => _referralAppService.GetMyReferralCodeAsync(tenantId))).Code;
        }

        using (LoginAs(refereeId))
        {
            await WithUnitOfWorkAsync(() => _membershipAppService.JoinAsync(new JoinBusinessDto { TenantId = tenantId, ReferralCode = referralCode }));
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var referrals = await _referralRepository.GetByReferrerMembershipIdsAsync(new[] { referrerMembershipId }.ToList());
                referrals.ShouldHaveSingleItem();
                referrals.Single().RefereeCustomerId.ShouldBe(refereeId);
            }
        });
    }

    [Fact]
    public async Task JoinAsync_Should_Ignore_An_Unknown_Referral_Code_Without_Failing_The_Join()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();

        using (LoginAs(customerId))
        {
            var membership = await WithUnitOfWorkAsync(() =>
                _membershipAppService.JoinAsync(new JoinBusinessDto { TenantId = tenantId, ReferralCode = "NOTREAL1" }));
            membership.CustomerId.ShouldBe(customerId);
        }
    }

}
