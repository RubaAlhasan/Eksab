using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.Reporting;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Billing;

[RemoteService(IsEnabled = false)]
public class AdminSubscriptionAppService : ApplicationService, IAdminSubscriptionAppService
{
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IRepository<Payment, Guid> _paymentRepository;
    private readonly IDataFilter _dataFilter;

    public AdminSubscriptionAppService(
        ITenantSubscriptionRepository subscriptionRepository,
        ISubscriptionPlanRepository planRepository,
        IInvoiceRepository invoiceRepository,
        IRepository<Payment, Guid> paymentRepository,
        IDataFilter dataFilter)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _invoiceRepository = invoiceRepository;
        _paymentRepository = paymentRepository;
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
