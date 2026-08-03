using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
                sorting: input.Sorting,
                skipCount: input.SkipCount,
                maxResultCount: input.MaxResultCount);

            var dtos = ObjectMapper.Map<List<TenantSubscription>, List<TenantSubscriptionDto>>(subscriptions);
            await SetPlanNamesAsync(dtos);
            return new PagedResultDto<TenantSubscriptionDto>(totalCount, dtos);
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
