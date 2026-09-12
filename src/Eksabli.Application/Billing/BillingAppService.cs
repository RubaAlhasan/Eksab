using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Branches;
using Eksabli.Features;
using Eksabli.Notifications;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Features;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace Eksabli.Billing;

[RemoteService(IsEnabled = false)]
public class BillingAppService : ApplicationService, IBillingAppService
{
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IRepository<Branch, Guid> _branchRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IdentityUserManager _identityUserManager;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ICurrentTenant _currentTenant;

    public BillingAppService(
        ITenantSubscriptionRepository subscriptionRepository,
        ISubscriptionPlanRepository planRepository,
        IInvoiceRepository invoiceRepository,
        IRepository<Branch, Guid> branchRepository,
        ITenantRepository tenantRepository,
        IdentityUserManager identityUserManager,
        INotificationPublisher notificationPublisher,
        ICurrentTenant currentTenant)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _invoiceRepository = invoiceRepository;
        _branchRepository = branchRepository;
        _tenantRepository = tenantRepository;
        _identityUserManager = identityUserManager;
        _notificationPublisher = notificationPublisher;
        _currentTenant = currentTenant;
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

    // Records a REQUEST only — does not touch PlanId or push any feature limits. A platform admin must
    // accept it (AdminSubscriptionAppService.ApprovePlanChangeAsync) before it actually takes effect;
    // see TenantSubscription.RequestPlanChange's own comment for why. This is a deliberate change from
    // this method's original behavior (immediate, unapproved plan swap) — see business-subscription
    // .component.ts's file comment for the UI-side history of that.
    public async Task<TenantSubscriptionDto> ChangePlanAsync(ChangePlanDto input)
    {
        var subscription = await _subscriptionRepository.SingleAsync();
        var plan = await _planRepository.GetAsync(input.PlanId);

        if (plan.Id == subscription.PlanId)
        {
            throw new UserFriendlyException("You're already on this plan.");
        }

        subscription.RequestPlanChange(plan.Id, Clock.Now);
        await _subscriptionRepository.UpdateAsync(subscription);

        var currentPlan = await _planRepository.FindAsync(subscription.PlanId);
        await NotifyAdminsOfPlanChangeRequestAsync(subscription, currentPlan?.Name, plan.Name);

        var dto = ObjectMapper.Map<TenantSubscription, TenantSubscriptionDto>(subscription);
        dto.PlanName = currentPlan?.Name;
        dto.PendingPlanName = plan.Name;
        return dto;
    }

    // Every Host-realm user in the "admin" role — the only Host role that actually exists today
    // (AdminPermissionDataSeederContributor grants the full permission set to "admin" specifically;
    // see its own comment on why the usual multi-role setup — Super Admin/Support Agent/Billing
    // Admin/Content Moderator — was never actually seeded). GetUsersInRoleAsync is stock ASP.NET Core
    // Identity, not a custom query, so this stays correct if that ever changes to real distinct roles —
    // just update the role name(s) checked here.
    private async Task NotifyAdminsOfPlanChangeRequestAsync(TenantSubscription subscription, string? currentPlanName, string requestedPlanName)
    {
        var tenantId = subscription.TenantId
            ?? throw new AbpException("A plan-change request must belong to a tenant's own subscription.");

        IList<IdentityUser> admins;
        using (_currentTenant.Change(null)) // admins are Host-realm
        {
            admins = await _identityUserManager.GetUsersInRoleAsync("admin");
        }

        var tenant = await _tenantRepository.FindAsync(tenantId);
        var tenantName = tenant?.Name ?? tenantId.ToString();

        foreach (var admin in admins)
        {
            await _notificationPublisher.PublishToUserAsync(
                admin.Id, tenantId: null, UserNotificationType.Info,
                title: "Plan change requested",
                message: $"{tenantName} requested to switch from {currentPlanName ?? "—"} to {requestedPlanName}.",
                category: "billing.plan_change_requested",
                data: new { subscriptionId = subscription.Id, tenantId, tenantName, currentPlanName, requestedPlanName });
        }
    }
}
