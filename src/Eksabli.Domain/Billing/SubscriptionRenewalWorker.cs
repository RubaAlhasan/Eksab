using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace Eksabli.Billing;

// Second AsyncPeriodicBackgroundWorkerBase in this repo (mirrors PointsExpirationWorker). Daily sweep
// that generates an Invoice for every subscription due for renewal, attempts a charge via
// IPaymentGateway, and advances the subscription on success / marks it PastDue on failure.
public class SubscriptionRenewalWorker : AsyncPeriodicBackgroundWorkerBase
{
    public SubscriptionRenewalWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = 24 * 60 * 60 * 1000; // daily
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var subscriptionRepository = workerContext.ServiceProvider.GetRequiredService<ITenantSubscriptionRepository>();
        var planRepository = workerContext.ServiceProvider.GetRequiredService<ISubscriptionPlanRepository>();
        var invoiceRepository = workerContext.ServiceProvider.GetRequiredService<IInvoiceRepository>();
        var paymentRepository = workerContext.ServiceProvider.GetRequiredService<IRepository<Payment, Guid>>();
        var paymentGateway = workerContext.ServiceProvider.GetRequiredService<IPaymentGateway>();
        var currentTenant = workerContext.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var dataFilter = workerContext.ServiceProvider.GetRequiredService<IDataFilter>();
        var unitOfWorkManager = workerContext.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var guidGenerator = workerContext.ServiceProvider.GetRequiredService<IGuidGenerator>();
        var clock = workerContext.ServiceProvider.GetRequiredService<IClock>();

        var now = clock.Now;
        List<TenantSubscription> due;
        using (dataFilter.Disable<IMultiTenant>())
        {
            due = await subscriptionRepository.GetListAsync(s =>
                s.RenewalDate <= now &&
                (s.Status == TenantSubscriptionStatus.Trialing || s.Status == TenantSubscriptionStatus.Active));
        }

        foreach (var subscription in due)
        {
            using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            using (currentTenant.Change(subscription.TenantId))
            {
                await RenewSubscriptionAsync(subscription, subscriptionRepository, planRepository, invoiceRepository, paymentRepository, paymentGateway, guidGenerator, clock);
            }
            await uow.CompleteAsync();
        }
    }

    private static async Task RenewSubscriptionAsync(
        TenantSubscription subscription,
        ITenantSubscriptionRepository subscriptionRepository,
        ISubscriptionPlanRepository planRepository,
        IInvoiceRepository invoiceRepository,
        IRepository<Payment, Guid> paymentRepository,
        IPaymentGateway paymentGateway,
        IGuidGenerator guidGenerator,
        IClock clock)
    {
        var plan = await planRepository.GetAsync(subscription.PlanId);
        var now = clock.Now;

        var invoice = Invoice.Create(guidGenerator.Create(), subscription.Id, plan.MonthlyPrice, now);
        await invoiceRepository.InsertAsync(invoice);

        var chargeResult = await paymentGateway.ChargeAsync(subscription.TenantId!.Value, plan.MonthlyPrice, $"Eksabli subscription renewal — {plan.Name}");

        var payment = Payment.Create(guidGenerator.Create(), invoice.Id, chargeResult.ProviderName);
        if (chargeResult.Succeeded)
        {
            payment.MarkSucceeded(chargeResult.ProviderTransactionRef);
        }
        else
        {
            payment.MarkFailed();
        }
        await paymentRepository.InsertAsync(payment);

        if (chargeResult.Succeeded)
        {
            invoice.MarkPaid(now);
            subscription.Renew(now.AddMonths(1));
            subscription.MarkActive();
        }
        else
        {
            invoice.MarkOverdue();
            subscription.MarkPastDue();
        }

        await invoiceRepository.UpdateAsync(invoice);
        await subscriptionRepository.UpdateAsync(subscription);
    }
}
