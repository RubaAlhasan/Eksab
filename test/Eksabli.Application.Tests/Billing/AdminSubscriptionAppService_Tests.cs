using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.Billing;

public abstract class AdminSubscriptionAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAdminSubscriptionAppService _adminSubscriptionAppService;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ICurrentTenant _currentTenant;

    protected AdminSubscriptionAppService_Tests()
    {
        _adminSubscriptionAppService = GetRequiredService<IAdminSubscriptionAppService>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _planRepository = GetRequiredService<ISubscriptionPlanRepository>();
        _subscriptionRepository = GetRequiredService<ITenantSubscriptionRepository>();
        _invoiceRepository = GetRequiredService<IInvoiceRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private async Task<(Guid TenantId, Guid SubscriptionId)> CreateTenantWithSubscriptionAsync()
    {
        Guid tenantId = default, subscriptionId = default;

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
                var plan = SubscriptionPlan.Create(Guid.NewGuid(), "Growth", 49m, "{}");
                await _planRepository.InsertAsync(plan, autoSave: true);

                var subscription = TenantSubscription.Create(Guid.NewGuid(), plan.Id, DateTime.UtcNow, DateTime.UtcNow.AddDays(14), TenantSubscriptionStatus.Trialing);
                await _subscriptionRepository.InsertAsync(subscription, autoSave: true);
                subscriptionId = subscription.Id;
            }
        });

        return (tenantId, subscriptionId);
    }

    [Fact]
    public async Task Should_List_Subscriptions_Across_All_Tenants()
    {
        var (tenantA, _) = await CreateTenantWithSubscriptionAsync();
        var (tenantB, _) = await CreateTenantWithSubscriptionAsync();

        var list = await WithUnitOfWorkAsync(() => _adminSubscriptionAppService.GetListAsync(new AdminSubscriptionFilterDto()));

        list.Items.Select(s => s.TenantId).ShouldContain(tenantA);
        list.Items.Select(s => s.TenantId).ShouldContain(tenantB);
    }

    [Fact]
    public async Task RecordManualPaymentAsync_Should_Mark_Invoice_Paid_And_Insert_A_Payment()
    {
        var (tenantId, subscriptionId) = await CreateTenantWithSubscriptionAsync();

        Guid invoiceId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var invoice = Invoice.Create(Guid.NewGuid(), subscriptionId, 49m, DateTime.UtcNow);
                await _invoiceRepository.InsertAsync(invoice, autoSave: true);
                invoiceId = invoice.Id;
            }
        });

        var result = await WithUnitOfWorkAsync(() => _adminSubscriptionAppService.RecordManualPaymentAsync(new RecordManualPaymentDto
        {
            InvoiceId = invoiceId,
            ProviderTransactionRef = "wire-12345"
        }));

        result.Status.ShouldBe(InvoiceStatus.Paid);
        result.PaidAt.ShouldNotBeNull();
    }
}
