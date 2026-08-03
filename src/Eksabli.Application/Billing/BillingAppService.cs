using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Eksabli.Branches;
using Eksabli.Features;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Features;

namespace Eksabli.Billing;

[RemoteService(IsEnabled = false)]
public class BillingAppService : ApplicationService, IBillingAppService
{
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IRepository<Branch, Guid> _branchRepository;
    private readonly IFeatureManager _featureManager;

    public BillingAppService(
        ITenantSubscriptionRepository subscriptionRepository,
        ISubscriptionPlanRepository planRepository,
        IInvoiceRepository invoiceRepository,
        IRepository<Branch, Guid> branchRepository,
        IFeatureManager featureManager)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _invoiceRepository = invoiceRepository;
        _branchRepository = branchRepository;
        _featureManager = featureManager;
    }

    public async Task<TenantSubscriptionDto> GetMyCurrentSubscriptionAsync()
    {
        var subscription = await _subscriptionRepository.SingleAsync();
        var dto = ObjectMapper.Map<TenantSubscription, TenantSubscriptionDto>(subscription);

        var plan = await _planRepository.FindAsync(subscription.PlanId);
        dto.PlanName = plan?.Name;
        return dto;
    }

    public async Task<UsageDto> GetMyUsageAsync()
    {
        var maxBranches = await FeatureChecker.GetAsync<int>(EksabliFeatures.MaxBranches);
        var branchCount = (int)await _branchRepository.GetCountAsync();

        return new UsageDto
        {
            BranchCount = branchCount,
            MaxBranches = maxBranches
        };
    }

    public async Task<PagedResultDto<InvoiceDto>> GetMyInvoicesAsync(PagedAndSortedResultRequestDto input)
    {
        var subscription = await _subscriptionRepository.SingleAsync();

        var (invoices, totalCount) = await _invoiceRepository.GetListAsync(
            tenantSubscriptionId: subscription.Id,
            sorting: input.Sorting,
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount);

        return new PagedResultDto<InvoiceDto>(totalCount, ObjectMapper.Map<List<Invoice>, List<InvoiceDto>>(invoices));
    }

    public async Task<TenantSubscriptionDto> ChangePlanAsync(ChangePlanDto input)
    {
        var subscription = await _subscriptionRepository.SingleAsync();
        var plan = await _planRepository.GetAsync(input.PlanId);

        subscription.ChangePlan(plan.Id);
        await _subscriptionRepository.UpdateAsync(subscription);

        await PushPlanFeaturesAsync(plan);

        var dto = ObjectMapper.Map<TenantSubscription, TenantSubscriptionDto>(subscription);
        dto.PlanName = plan.Name;
        return dto;
    }

    // Pushes SubscriptionPlan.FeatureLimitsJson into ABP Feature Management for the ambient tenant —
    // enforcement anywhere else in the codebase is a FeatureChecker read, not new business logic.
    private async Task PushPlanFeaturesAsync(SubscriptionPlan plan)
    {
        var tenantId = CurrentTenant.Id
            ?? throw new AbpException("PushPlanFeaturesAsync must run in a tenant context.");

        var limits = JsonSerializer.Deserialize<Dictionary<string, string>>(plan.FeatureLimitsJson) ?? new Dictionary<string, string>();
        foreach (var (key, value) in limits)
        {
            await _featureManager.SetForTenantAsync(tenantId, key, value);
        }
    }
}
