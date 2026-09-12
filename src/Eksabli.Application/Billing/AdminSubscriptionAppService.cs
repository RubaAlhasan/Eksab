using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.Notifications;
using Eksabli.Reporting;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.FeatureManagement;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Billing;

[RemoteService(IsEnabled = false)]
public class AdminSubscriptionAppService : ApplicationService, IAdminSubscriptionAppService
{
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IRepository<Payment, Guid> _paymentRepository;
    private readonly IFeatureManager _featureManager;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly IDataFilter _dataFilter;

    public AdminSubscriptionAppService(
        ITenantSubscriptionRepository subscriptionRepository,
        ISubscriptionPlanRepository planRepository,
        IInvoiceRepository invoiceRepository,
        IRepository<Payment, Guid> paymentRepository,
        IFeatureManager featureManager,
        INotificationPublisher notificationPublisher,
        IDataFilter dataFilter)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _invoiceRepository = invoiceRepository;
        _paymentRepository = paymentRepository;
        _featureManager = featureManager;
        _notificationPublisher = notificationPublisher;
        _dataFilter = dataFilter;
    }

    public async Task<PagedResultDto<TenantSubscriptionDto>> GetListAsync(AdminSubscriptionFilterDto input)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var (subscriptions, totalCount) = await _subscriptionRepository.GetListAsync(
                status: input.Status,
                tenantId: input.TenantId,
                sorting: input.Sorting,
                skipCount: input.SkipCount,
                maxResultCount: input.MaxResultCount);

            var dtos = ObjectMapper.Map<List<TenantSubscription>, List<TenantSubscriptionDto>>(subscriptions);
            await SetPlanNamesAsync(dtos);
            return new PagedResultDto<TenantSubscriptionDto>(totalCount, dtos);
        }
    }

    // Replaces what admin-subscriptions.component.ts used to compute client-side from two separate
    // GetListAsync calls (status=Active with up to 500 items transferred, status=Trialing count-only) —
    // one round trip instead of two, and a TRUE total (every active subscription grouped by plan at
    // the DB level, not the old client-side version's first-500-rows cap on the MRR sum).
    public async Task<AdminSubscriptionStatsDto> GetStatsAsync()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var queryable = await _subscriptionRepository.GetQueryableAsync();

            var activeByPlan = await AsyncExecuter.ToListAsync(
                queryable
                    .Where(s => s.Status == TenantSubscriptionStatus.Active)
                    .GroupBy(s => s.PlanId)
                    .Select(g => new { PlanId = g.Key, Count = g.Count() }));

            var trialingCount = await AsyncExecuter.CountAsync(
                queryable.Where(s => s.Status == TenantSubscriptionStatus.Trialing));

            var planPrices = await AsyncExecuter.ToListAsync(
                (await _planRepository.GetQueryableAsync()).Select(p => new { p.Id, p.MonthlyPrice }));
            var priceByPlanId = planPrices.ToDictionary(p => p.Id, p => p.MonthlyPrice);

            return new AdminSubscriptionStatsDto
            {
                ActiveCount = activeByPlan.Sum(x => x.Count),
                TrialingCount = trialingCount,
                ApproxMrr = activeByPlan.Sum(x => priceByPlanId.GetValueOrDefault(x.PlanId) * x.Count)
            };
        }
    }

    // Real, DB-backed revenue trend for the Admin Dashboard's "Platform MRR" chart (see
    // MrrTrendPointDto for why this is collected-revenue-per-month rather than a true point-in-time
    // MRR snapshot) — one bar per month for the trailing 7 months (this month inclusive), zero-filled
    // for months with no paid invoices rather than omitted, so a sparse trend doesn't misrepresent
    // itself as a shorter one.
    public async Task<List<MrrTrendPointDto>> GetMrrTrendAsync()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var months = TrailingMonths.Compute(Clock.Now);
            var from = new DateTime(months[0].Year, months[0].Month, 1);

            var queryable = await _invoiceRepository.GetQueryableAsync();
            var paidInvoices = await AsyncExecuter.ToListAsync(
                queryable.Where(i => i.Status == InvoiceStatus.Paid && i.PaidAt != null && i.PaidAt >= from));

            var amountByMonth = paidInvoices
                .GroupBy(i => new { i.PaidAt!.Value.Year, i.PaidAt!.Value.Month })
                .ToDictionary(g => (g.Key.Year, g.Key.Month), g => g.Sum(i => i.Amount));

            return months
                .Select(m => new MrrTrendPointDto
                {
                    Year = m.Year,
                    Month = m.Month,
                    Amount = amountByMonth.GetValueOrDefault((m.Year, m.Month))
                })
                .ToList();
        }
    }

    public async Task<PagedResultDto<InvoiceDto>> GetInvoicesAsync(AdminInvoiceFilterDto input)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var (invoices, totalCount) = await _invoiceRepository.GetListAsync(
                tenantSubscriptionId: input.TenantSubscriptionId,
                status: input.Status,
                sorting: input.Sorting,
                skipCount: input.SkipCount,
                maxResultCount: input.MaxResultCount);

            return new PagedResultDto<InvoiceDto>(totalCount, ObjectMapper.Map<List<Invoice>, List<InvoiceDto>>(invoices));
        }
    }

    public async Task<InvoiceDto> RecordManualPaymentAsync(RecordManualPaymentDto input)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var invoice = await _invoiceRepository.GetAsync(input.InvoiceId);

            var payment = Payment.Create(GuidGenerator.Create(), invoice.Id, "Manual");
            payment.MarkSucceeded(input.ProviderTransactionRef);
            await _paymentRepository.InsertAsync(payment);

            invoice.MarkPaid(Clock.Now);
            await _invoiceRepository.UpdateAsync(invoice);

            return ObjectMapper.Map<Invoice, InvoiceDto>(invoice);
        }
    }

    // The other half of Billing.BillingAppService.ChangePlanAsync — that call only ever records a
    // request now (see its own comment); this is what actually applies it. Real hard-delete-style
    // "no going back" mutation: reassigns PlanId, clears the pending fields, pushes the new plan's
    // feature limits, and marks the subscription Active — see TenantSubscription
    // .ApprovePendingPlanChange's own comment for why activation is bundled in here rather than a
    // separate step.
    public async Task<TenantSubscriptionDto> ApprovePlanChangeAsync(Guid id)
    {
        TenantSubscriptionDto dto;
        Guid tenantId;
        string newPlanName;

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var subscription = await _subscriptionRepository.GetAsync(id);
            if (!subscription.PendingPlanId.HasValue)
            {
                throw new UserFriendlyException("This subscription has no pending plan change to approve.");
            }

            var newPlan = await _planRepository.GetAsync(subscription.PendingPlanId.Value);
            subscription.ApprovePendingPlanChange();
            await _subscriptionRepository.UpdateAsync(subscription);

            await PushPlanFeaturesAsync(subscription.TenantId!.Value, newPlan);

            dto = ObjectMapper.Map<TenantSubscription, TenantSubscriptionDto>(subscription);
            dto.PlanName = newPlan.Name;
            tenantId = subscription.TenantId!.Value;
            newPlanName = newPlan.Name;
        }

        // Fired AFTER the Disable<IMultiTenant>() scope above closes — PublishToTenantAsync switches
        // ambient tenant to `tenantId` itself and needs the ordinary (enabled) filter in effect to scope
        // its own user lookup to just this tenant; calling it from inside the disabled scope would fan
        // this notification out to every user platform-wide instead.
        await _notificationPublisher.PublishToTenantAsync(
            tenantId, UserNotificationType.Success,
            title: "Plan change approved",
            message: $"Your request to switch to {newPlanName} was approved — your subscription is now active.",
            category: "billing.plan_change_approved",
            data: new { subscriptionId = id, planName = newPlanName });

        return dto;
    }

    public async Task<TenantSubscriptionDto> RejectPlanChangeAsync(Guid id)
    {
        TenantSubscriptionDto dto;
        Guid tenantId;
        string rejectedPlanName;

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var subscription = await _subscriptionRepository.GetAsync(id);
            if (!subscription.PendingPlanId.HasValue)
            {
                throw new UserFriendlyException("This subscription has no pending plan change to reject.");
            }

            var rejectedPlan = await _planRepository.FindAsync(subscription.PendingPlanId.Value);
            rejectedPlanName = rejectedPlan?.Name ?? "the requested plan";

            subscription.RejectPendingPlanChange();
            await _subscriptionRepository.UpdateAsync(subscription);

            dto = ObjectMapper.Map<TenantSubscription, TenantSubscriptionDto>(subscription);
            var currentPlan = await _planRepository.FindAsync(subscription.PlanId);
            dto.PlanName = currentPlan?.Name;
            tenantId = subscription.TenantId!.Value;
        }

        // Same "notify after the disabled-filter scope closes" reasoning as ApprovePlanChangeAsync above.
        await _notificationPublisher.PublishToTenantAsync(
            tenantId, UserNotificationType.Warning,
            title: "Plan change rejected",
            message: $"Your request to switch to {rejectedPlanName} was not approved. You're still on your current plan.",
            category: "billing.plan_change_rejected",
            data: new { subscriptionId = id, rejectedPlanName });

        return dto;
    }

    // Pushes SubscriptionPlan.FeatureLimitsJson into ABP Feature Management for an explicit tenant —
    // this Host-side call knows which tenant from the subscription itself, unlike the old tenant-side
    // BillingAppService version this replaces, which read CurrentTenant.Id (this runs with no ambient
    // tenant, Disable<IMultiTenant>() above).
    private async Task PushPlanFeaturesAsync(Guid tenantId, SubscriptionPlan plan)
    {
        var limits = SubscriptionPlanFeatureLimits.Parse(plan.FeatureLimitsJson);
        foreach (var (key, value) in limits)
        {
            await _featureManager.SetForTenantAsync(tenantId, key, value);
        }
    }

    // Closes the gap where RecordManualPaymentAsync writes a Payment row with no way to read it back —
    // Payment has no dedicated repository (deliberately reached only through Invoice/this service, see
    // the entity's own comment), so this queries the generic IRepository<Payment, Guid> directly, same
    // DB-level-paging shape as GetStatsAsync/GetMrrTrendAsync above.
    public async Task<PagedResultDto<PaymentDto>> GetPaymentsAsync(AdminPaymentFilterDto input)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var queryable = await _paymentRepository.GetQueryableAsync();

            if (input.InvoiceId.HasValue)
            {
                queryable = queryable.Where(p => p.InvoiceId == input.InvoiceId.Value);
            }

            if (input.Status.HasValue)
            {
                queryable = queryable.Where(p => p.Status == input.Status.Value);
            }

            var totalCount = await AsyncExecuter.CountAsync(queryable);

            var payments = await AsyncExecuter.ToListAsync(
                queryable
                    .OrderByDescending(p => p.CreationTime)
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount));

            return new PagedResultDto<PaymentDto>(totalCount, ObjectMapper.Map<List<Payment>, List<PaymentDto>>(payments));
        }
    }

    private async Task SetPlanNamesAsync(List<TenantSubscriptionDto> dtos)
    {
        if (dtos.Count == 0)
        {
            return;
        }

        var planIds = dtos.Select(d => d.PlanId).Distinct().ToList();
        var plans = await _planRepository.GetListAsync(p => planIds.Contains(p.Id));
        var lookup = plans.ToDictionary(p => p.Id, p => p.Name);

        foreach (var dto in dtos)
        {
            dto.PlanName = lookup.GetValueOrDefault(dto.PlanId);
        }
    }
}
