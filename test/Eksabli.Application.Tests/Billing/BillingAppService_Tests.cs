using System;
using System.Threading.Tasks;
using Eksabli.Branches;
using Eksabli.Features;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Features;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.Billing;

public abstract class BillingAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IBillingAppService _billingAppService;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly IRepository<Branch, Guid> _branchRepository;
    private readonly IFeatureManager _featureManager;
    private readonly IFeatureChecker _featureChecker;
    private readonly ICurrentTenant _currentTenant;

    protected BillingAppService_Tests()
    {
        _billingAppService = GetRequiredService<IBillingAppService>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _planRepository = GetRequiredService<ISubscriptionPlanRepository>();
        _subscriptionRepository = GetRequiredService<ITenantSubscriptionRepository>();
        _branchRepository = GetRequiredService<IRepository<Branch, Guid>>();
        _featureManager = GetRequiredService<IFeatureManager>();
        _featureChecker = GetRequiredService<IFeatureChecker>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private async Task<(Guid TenantId, Guid PlanId, Guid SubscriptionId)> CreateTenantWithSubscriptionAsync(string planName, string maxBranches)
    {
        Guid tenantId = default, planId = default, subscriptionId = default;

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
                var plan = SubscriptionPlan.Create(Guid.NewGuid(), planName, 49m, $"{{\"{EksabliFeatures.MaxBranches}\":\"{maxBranches}\"}}");
                await _planRepository.InsertAsync(plan, autoSave: true);
                planId = plan.Id;

                var subscription = TenantSubscription.Create(Guid.NewGuid(), plan.Id, DateTime.UtcNow, DateTime.UtcNow.AddDays(14), TenantSubscriptionStatus.Trialing);
                await _subscriptionRepository.InsertAsync(subscription, autoSave: true);
                subscriptionId = subscription.Id;

                await _featureManager.SetForTenantAsync(tenantId, EksabliFeatures.MaxBranches, maxBranches);
            }
        });

        return (tenantId, planId, subscriptionId);
    }

    [Fact]
    public async Task Should_Get_My_Current_Subscription_With_Plan_Name()
    {
        var (tenantId, _, _) = await CreateTenantWithSubscriptionAsync("Growth", "5");

        using (_currentTenant.Change(tenantId))
        {
            var dto = await WithUnitOfWorkAsync(() => _billingAppService.GetMyCurrentSubscriptionAsync());
            dto.PlanName.ShouldBe("Growth");
            dto.Status.ShouldBe(TenantSubscriptionStatus.Trialing);
        }
    }

    [Fact]
    public async Task Should_Report_Usage_Against_The_Plan_Limit()
    {
        var (tenantId, _, _) = await CreateTenantWithSubscriptionAsync("Starter", "1");

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                await _branchRepository.InsertAsync(Branch.Create(Guid.NewGuid(), "Main"), autoSave: true);
            }
        });

        using (_currentTenant.Change(tenantId))
        {
            var usage = await WithUnitOfWorkAsync(() => _billingAppService.GetMyUsageAsync());
            usage.BranchCount.ShouldBe(1);
            usage.MaxBranches.ShouldBe(1);
        }
    }

    [Fact]
    public async Task ChangePlanAsync_Should_Update_Plan_And_Push_Its_Features()
    {
        var (tenantId, _, _) = await CreateTenantWithSubscriptionAsync("Starter", "1");

        Guid newPlanId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var growthPlan = SubscriptionPlan.Create(Guid.NewGuid(), "Growth", 149m, $"{{\"{EksabliFeatures.MaxBranches}\":\"5\"}}");
                await _planRepository.InsertAsync(growthPlan, autoSave: true);
                newPlanId = growthPlan.Id;
            }
        });

        using (_currentTenant.Change(tenantId))
        {
            var dto = await WithUnitOfWorkAsync(() => _billingAppService.ChangePlanAsync(new ChangePlanDto { PlanId = newPlanId }));
            dto.PlanName.ShouldBe("Growth");

            var maxBranches = await _featureChecker.GetAsync<int>(EksabliFeatures.MaxBranches);
            maxBranches.ShouldBe(5);
        }
    }
}
