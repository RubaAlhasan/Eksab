using System;
using System.Threading.Tasks;
using Eksabli.Billing;
using Eksabli.BusinessProfiles;
using Eksabli.Branches;
using Eksabli.EmployeeAssignments;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.Businesses;

public abstract class BusinessAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IBusinessAppService _businessAppService;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<BusinessProfile, Guid> _businessProfileRepository;
    private readonly IRepository<Branch, Guid> _branchRepository;
    private readonly IRepository<EmployeeAssignment, Guid> _employeeAssignmentRepository;
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly ITenantSubscriptionRepository _tenantSubscriptionRepository;
    private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;
    private readonly ICurrentTenant _currentTenant;

    protected BusinessAppService_Tests()
    {
        _businessAppService = GetRequiredService<IBusinessAppService>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _businessProfileRepository = GetRequiredService<IRepository<BusinessProfile, Guid>>();
        _branchRepository = GetRequiredService<IRepository<Branch, Guid>>();
        _employeeAssignmentRepository = GetRequiredService<IRepository<EmployeeAssignment, Guid>>();
        _identityUserRepository = GetRequiredService<IIdentityUserRepository>();
        _tenantSubscriptionRepository = GetRequiredService<ITenantSubscriptionRepository>();
        _subscriptionPlanRepository = GetRequiredService<ISubscriptionPlanRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private static RegisterBusinessDto CreateInput(string businessName) => new RegisterBusinessDto
    {
        BusinessName = businessName,
        BranchName = "Main Branch",
        BranchAddress = "123 Street",
        OwnerEmail = $"owner-{Guid.NewGuid():N}@example.com",
        OwnerPassword = "1q2w3E*"
    };

    [Fact]
    public async Task Should_Register_New_Business_And_Create_Tenant_BusinessProfile_Branch_Owner_EmployeeAssignment()
    {
        var input = CreateInput("Bloom & Brew " + Guid.NewGuid().ToString("N"));

        var result = await WithUnitOfWorkAsync(() => _businessAppService.RegisterAsync(input));

        result.TenantId.ShouldNotBe(Guid.Empty);
        result.BusinessProfileId.ShouldNotBe(Guid.Empty);
        result.BranchId.ShouldNotBe(Guid.Empty);
        result.OwnerUserId.ShouldNotBe(Guid.Empty);

        await WithUnitOfWorkAsync(async () =>
        {
            var tenant = await _tenantRepository.GetAsync(result.TenantId);
            tenant.Name.ShouldBe(input.BusinessName);

            using (_currentTenant.Change(result.TenantId))
            {
                var profile = await _businessProfileRepository.GetAsync(result.BusinessProfileId);
                profile.TenantId.ShouldBe(result.TenantId);

                var branch = await _branchRepository.GetAsync(result.BranchId);
                branch.Name.ShouldBe(input.BranchName);

                var assignment = await _employeeAssignmentRepository.SingleAsync(x => x.UserId == result.OwnerUserId);
                assignment.Role.ShouldBe(EmployeeRole.Owner);

                var ownerUser = await _identityUserRepository.GetAsync(result.OwnerUserId);
                ownerUser.Email.ShouldBe(input.OwnerEmail);
            }
        });
    }

    [Fact]
    public async Task Should_Provision_A_Trialing_Subscription_On_The_TrialDefault_Plan()
    {
        var input = CreateInput("Trial Test Biz " + Guid.NewGuid().ToString("N"));

        var result = await WithUnitOfWorkAsync(() => _businessAppService.RegisterAsync(input));

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(result.TenantId))
            {
                var subscription = await _tenantSubscriptionRepository.SingleAsync();
                subscription.Status.ShouldBe(Billing.TenantSubscriptionStatus.Trialing);

                var plan = await _subscriptionPlanRepository.GetAsync(subscription.PlanId);
                plan.IsTrialDefault.ShouldBeTrue();
            }
        });
    }

    [Fact]
    public async Task Should_Not_Register_Business_With_Duplicate_Name()
    {
        var businessName = "Crust & Co " + Guid.NewGuid().ToString("N");
        var input = CreateInput(businessName);

        await WithUnitOfWorkAsync(() => _businessAppService.RegisterAsync(input));

        var duplicateInput = CreateInput(businessName);

        await Assert.ThrowsAsync<BusinessException>(async () =>
        {
            await WithUnitOfWorkAsync(() => _businessAppService.RegisterAsync(duplicateInput));
        });
    }

    [Fact]
    public async Task Should_Isolate_Two_Registered_Businesses_From_Each_Other()
    {
        var inputA = CreateInput("FitLine Sports " + Guid.NewGuid().ToString("N"));
        var inputB = CreateInput("Pizza Shop " + Guid.NewGuid().ToString("N"));

        var resultA = await WithUnitOfWorkAsync(() => _businessAppService.RegisterAsync(inputA));
        var resultB = await WithUnitOfWorkAsync(() => _businessAppService.RegisterAsync(inputB));

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(resultA.TenantId))
            {
                var profile = await _businessProfileRepository.SingleAsync();
                profile.Id.ShouldBe(resultA.BusinessProfileId);
            }

            using (_currentTenant.Change(resultB.TenantId))
            {
                var profile = await _businessProfileRepository.SingleAsync();
                profile.Id.ShouldBe(resultB.BusinessProfileId);
            }
        });
    }
}
