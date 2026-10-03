using System;
using System.Reflection;
using System.Threading.Tasks;
using Eksabli.Billing;
using Eksabli.Shared;
using Shouldly;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Billing;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class SubscriptionRenewalWorker_Tests : EksabliEntityFrameworkCoreTestBase
{
    private readonly SubscriptionRenewalWorker _worker;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IRepository<Payment, Guid> _paymentRepository;
    private readonly ICurrentTenant _currentTenant;

    public SubscriptionRenewalWorker_Tests()
    {
        _worker = GetRequiredService<SubscriptionRenewalWorker>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _planRepository = GetRequiredService<ISubscriptionPlanRepository>();
        _subscriptionRepository = GetRequiredService<ITenantSubscriptionRepository>();
        _invoiceRepository = GetRequiredService<IInvoiceRepository>();
        _paymentRepository = GetRequiredService<IRepository<Payment, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private async Task RunWorkerOnceAsync()
    {
        var method = typeof(SubscriptionRenewalWorker).GetMethod("DoWorkAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var context = new PeriodicBackgroundWorkerContext(ServiceProvider);
        await (Task)method.Invoke(_worker, new object[] { context })!;
    }

    private async Task<Guid> CreateTenantAsync()
    {
        Guid tenantId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            var tenant = await _tenantManager.CreateAsync("tenant-" + Guid.NewGuid().ToString("N"));
            await _tenantRepository.InsertAsync(tenant, autoSave: true);
            tenantId = tenant.Id;
        });
        return tenantId;
    }

    [Fact]
    public async Task Should_Renew_A_Due_Subscription_Charge_Successfully_And_Convert_Trial_To_Active()
    {
        var tenantId = await CreateTenantAsync();
        Guid subscriptionId = default;
        var originalRenewalDate = DateTime.UtcNow.AddDays(-1);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var plan = SubscriptionPlan.Create(Guid.NewGuid(), "Growth", 49m, 5m, "{}");
                await _planRepository.InsertAsync(plan, autoSave: true);

                var subscription = TenantSubscription.Create(
                    Guid.NewGuid(), plan.Id, DateTime.UtcNow.AddDays(-14), originalRenewalDate, TenantSubscriptionStatus.Trialing, Currency.Syp);
                await _subscriptionRepository.InsertAsync(subscription, autoSave: true);
                subscriptionId = subscription.Id;
            }
        });

        await RunWorkerOnceAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var subscription = await _subscriptionRepository.GetAsync(subscriptionId);
                subscription.Status.ShouldBe(TenantSubscriptionStatus.Active);
                subscription.RenewalDate.ShouldBeGreaterThan(originalRenewalDate);

                var invoices = await _invoiceRepository.GetListAsync(t => t.TenantSubscriptionId == subscriptionId);
                invoices.Count.ShouldBe(1);
                invoices[0].Status.ShouldBe(InvoiceStatus.Paid);
                invoices[0].Amount.ShouldBe(49m);
                invoices[0].Currency.ShouldBe(Currency.Syp);

                var payments = await _paymentRepository.GetListAsync(p => p.InvoiceId == invoices[0].Id);
                payments.Count.ShouldBe(1);
                payments[0].Status.ShouldBe(PaymentStatus.Succeeded);
                payments[0].Provider.ShouldBe("Null");
            }
        });
    }

    // The one real behavioral risk in this change: picking the wrong price column for the tenant's
    // own billing currency. A USD-billed subscription's invoice must use SubscriptionPlan.MonthlyPriceUsd
    // (5m here), never MonthlyPriceSyp (49m).
    [Fact]
    public async Task Should_Renew_A_Usd_Subscription_Using_The_Usd_Price()
    {
        var tenantId = await CreateTenantAsync();
        Guid subscriptionId = default;
        var originalRenewalDate = DateTime.UtcNow.AddDays(-1);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var plan = SubscriptionPlan.Create(Guid.NewGuid(), "Growth", 49m, 5m, "{}");
                await _planRepository.InsertAsync(plan, autoSave: true);

                var subscription = TenantSubscription.Create(
                    Guid.NewGuid(), plan.Id, DateTime.UtcNow.AddDays(-14), originalRenewalDate, TenantSubscriptionStatus.Trialing, Currency.Usd);
                await _subscriptionRepository.InsertAsync(subscription, autoSave: true);
                subscriptionId = subscription.Id;
            }
        });

        await RunWorkerOnceAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var invoices = await _invoiceRepository.GetListAsync(t => t.TenantSubscriptionId == subscriptionId);
                invoices.Count.ShouldBe(1);
                invoices[0].Amount.ShouldBe(5m);
                invoices[0].Currency.ShouldBe(Currency.Usd);
            }
        });
    }
}
