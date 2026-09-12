using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Eksabli.Memberships;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.Engagement;

// Covers what changed when Membership.ReferralCode replaced handing out the raw Membership.Id as a
// "referral code" — see that property's own comment for the full reasoning. A raw GUID worked as a
// lookup key but was never something a real person could read out, type, or text to a friend.
public abstract class ReferralAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IReferralAppService _referralAppService;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    protected ReferralAppService_Tests()
    {
        _referralAppService = GetRequiredService<IReferralAppService>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _membershipRepository = GetRequiredService<IRepository<Membership, Guid>>();
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

    private async Task<Guid> JoinBusinessAsync(Guid tenantId, Guid customerId)
    {
        Guid membershipId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var membership = Membership.Create(Guid.NewGuid(), customerId, DateTime.UtcNow);
                await _membershipRepository.InsertAsync(membership, autoSave: true);
                membershipId = membership.Id;
            }
        });
        return membershipId;
    }

    [Fact]
    public async Task GetMyReferralCodeAsync_Should_Return_A_Short_Human_Typeable_Code_Not_The_Raw_MembershipId()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        var membershipId = await JoinBusinessAsync(tenantId, customerId);

        using (LoginAs(customerId))
        {
            var result = await WithUnitOfWorkAsync(() => _referralAppService.GetMyReferralCodeAsync(tenantId));

            result.Code.Length.ShouldBe(ReferralConsts.CodeLength);
            result.Code.ShouldNotBe(membershipId.ToString()); // not the raw Guid.ToString() either
            Guid.TryParse(result.Code, out _).ShouldBeFalse(); // genuinely not parseable back as a Guid
        }
    }

    [Fact]
    public async Task GetMyReferralCodeAsync_Should_Return_The_Same_Code_On_Every_Call()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinBusinessAsync(tenantId, customerId);

        using (LoginAs(customerId))
        {
            var first = await WithUnitOfWorkAsync(() => _referralAppService.GetMyReferralCodeAsync(tenantId));
            var second = await WithUnitOfWorkAsync(() => _referralAppService.GetMyReferralCodeAsync(tenantId));

            second.Code.ShouldBe(first.Code);
        }
    }

    [Fact]
    public async Task GetMyReferralCodeAsync_Should_Give_Different_Members_Different_Codes()
    {
        var tenantId = await CreateTenantAsync();
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        await JoinBusinessAsync(tenantId, customerA);
        await JoinBusinessAsync(tenantId, customerB);

        string codeA, codeB;
        using (LoginAs(customerA))
        {
            codeA = (await WithUnitOfWorkAsync(() => _referralAppService.GetMyReferralCodeAsync(tenantId))).Code;
        }
        using (LoginAs(customerB))
        {
            codeB = (await WithUnitOfWorkAsync(() => _referralAppService.GetMyReferralCodeAsync(tenantId))).Code;
        }

        codeA.ShouldNotBe(codeB);
    }
}
