using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Eksabli.BusinessProfiles;
using Eksabli.Memberships;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.Businesses;

public abstract class AdminTenantAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAdminTenantAppService _adminTenantAppService;
    private readonly IMembershipAppService _membershipAppService;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<BusinessProfile, Guid> _businessProfileRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    protected AdminTenantAppService_Tests()
    {
        _adminTenantAppService = GetRequiredService<IAdminTenantAppService>();
        _membershipAppService = GetRequiredService<IMembershipAppService>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _businessProfileRepository = GetRequiredService<IRepository<BusinessProfile, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private IDisposable LoginAs(Guid userId)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(AbpClaimTypes.UserId, userId.ToString()));
        return _currentPrincipalAccessor.Change(new ClaimsPrincipal(identity));
    }

    private async Task<Guid> CreateTenantWithBusinessProfileAsync()
    {
        Guid tenantId = default;

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
                await _businessProfileRepository.InsertAsync(BusinessProfile.Create(Guid.NewGuid()), autoSave: true);
            }
        });

        return tenantId;
    }

    [Fact]
    public async Task New_Tenant_Should_Start_Pending()
    {
        var tenantId = await CreateTenantWithBusinessProfileAsync();

        var dto = await WithUnitOfWorkAsync(() => _adminTenantAppService.GetAsync(tenantId));

        dto.ApprovalStatus.ShouldBe(TenantApprovalStatus.Pending);
    }

    [Fact]
    public async Task ApproveAsync_Then_SuspendAsync_Should_Update_Status()
    {
        var tenantId = await CreateTenantWithBusinessProfileAsync();

        var approved = await WithUnitOfWorkAsync(() => _adminTenantAppService.ApproveAsync(tenantId));
        approved.ApprovalStatus.ShouldBe(TenantApprovalStatus.Approved);

        var suspended = await WithUnitOfWorkAsync(() => _adminTenantAppService.SuspendAsync(tenantId));
        suspended.ApprovalStatus.ShouldBe(TenantApprovalStatus.Suspended);
    }

    [Fact]
    public async Task Suspended_Business_Should_Reject_New_Members()
    {
        var tenantId = await CreateTenantWithBusinessProfileAsync();
        await WithUnitOfWorkAsync(() => _adminTenantAppService.SuspendAsync(tenantId));

        using (LoginAs(Guid.NewGuid()))
        {
            await Should.ThrowAsync<UserFriendlyException>(() => WithUnitOfWorkAsync(() =>
                _membershipAppService.JoinAsync(new JoinBusinessDto { TenantId = tenantId })));
        }
    }
}
