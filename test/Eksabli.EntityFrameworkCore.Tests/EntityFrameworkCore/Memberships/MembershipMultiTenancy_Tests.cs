using System;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.Memberships;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Memberships;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class MembershipMultiTenancy_Tests : EksabliEntityFrameworkCoreTestBase
{
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    public MembershipMultiTenancy_Tests()
    {
        _membershipRepository = GetRequiredService<IRepository<Membership, Guid>>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    [Fact]
    public async Task Should_Isolate_Tenant_Scoped_Queries_And_Allow_CrossTenant_Customer_Queries()
    {
        var customerId = Guid.NewGuid();
        Guid tenantAId = default, tenantBId = default;

        // Arrange: create two real tenants (host-scoped — CurrentTenant is null here)
        await WithUnitOfWorkAsync(async () =>
        {
            var tenantA = await _tenantManager.CreateAsync("tenant-a-" + Guid.NewGuid());
            await _tenantRepository.InsertAsync(tenantA, autoSave: true);

            var tenantB = await _tenantManager.CreateAsync("tenant-b-" + Guid.NewGuid());
            await _tenantRepository.InsertAsync(tenantB, autoSave: true);

            tenantAId = tenantA.Id;
            tenantBId = tenantB.Id;
        });

        // Arrange: same customer joins both tenants, each Membership created while
        // CurrentTenant.Change(...) is active so TenantId gets populated by the framework
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantAId))
            {
                await _membershipRepository.InsertAsync(
                    Membership.Create(Guid.NewGuid(), customerId, DateTime.UtcNow), autoSave: true);
            }
        });

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantBId))
            {
                await _membershipRepository.InsertAsync(
                    Membership.Create(Guid.NewGuid(), customerId, DateTime.UtcNow), autoSave: true);
            }
        });

        // Act + Assert (a): tenant-scoped read sees only its own tenant's membership —
        // the automatic IMultiTenant filter, no manual WHERE TenantId=... anywhere.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantAId))
            {
                var visibleToA = await _membershipRepository.GetListAsync();
                visibleToA.Count.ShouldBe(1);
                visibleToA.Single().TenantId.ShouldBe(tenantAId);
                visibleToA.Single().CustomerId.ShouldBe(customerId);
            }
        });

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantBId))
            {
                var visibleToB = await _membershipRepository.GetListAsync();
                visibleToB.Count.ShouldBe(1);
                visibleToB.Single().TenantId.ShouldBe(tenantBId);
            }
        });

        // Act + Assert (b): host/customer-scoped read — filter disabled, CustomerId
        // filter substituted instead — sees BOTH memberships across both tenants.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var allForCustomer = await _membershipRepository.GetListAsync(m => m.CustomerId == customerId);

                allForCustomer.Count.ShouldBe(2);
                allForCustomer.Select(m => m.TenantId).ShouldBe(new Guid?[] { tenantAId, tenantBId }, ignoreOrder: true);
            }
        });
    }
}
