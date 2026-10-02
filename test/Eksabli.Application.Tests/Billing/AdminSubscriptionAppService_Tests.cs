using System;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.Features;
using Eksabli.Notifications;
using Eksabli.Shared;
using Microsoft.AspNetCore.Identity;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Features;
using Volo.Abp.Identity;
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
    private readonly IFeatureChecker _featureChecker;
    private readonly IdentityUserManager _userManager;
    private readonly IUserNotificationRepository _userNotificationRepository;
    private readonly ICurrentTenant _currentTenant;

    protected AdminSubscriptionAppService_Tests()
    {
        _adminSubscriptionAppService = GetRequiredService<IAdminSubscriptionAppService>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _planRepository = GetRequiredService<ISubscriptionPlanRepository>();
        _subscriptionRepository = GetRequiredService<ITenantSubscriptionRepository>();
        _invoiceRepository = GetRequiredService<IInvoiceRepository>();
        _featureChecker = GetRequiredService<IFeatureChecker>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _userNotificationRepository = GetRequiredService<IUserNotificationRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    // A bare tenant-realm IdentityUser (no EmployeeAssignment needed) — enough for
    // NotificationPublisher.PublishToTenantAsync's own fan-out query, which only cares about "which
    // users belong to this tenant", the same generic IIdentityUserRepository.GetListAsync() it uses.
    private async Task<Guid> CreateTenantStaffAsync(Guid tenantId)
    {
        Guid userId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var email = $"{Guid.NewGuid():N}@example.com";
                var user = new IdentityUser(Guid.NewGuid(), email, email, tenantId);
                (await _userManager.CreateAsync(user)).CheckErrors();
                userId = user.Id;
            }
        });
        return userId;
    }

    private async Task<(Guid TenantId, Guid SubscriptionId)> CreateTenantWithSubscriptionAsync(Currency currency = Currency.Syp)
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
                var plan = SubscriptionPlan.Create(Guid.NewGuid(), "Growth", 49m, 5m, "{}");
                await _planRepository.InsertAsync(plan, autoSave: true);

                var subscription = TenantSubscription.Create(Guid.NewGuid(), plan.Id, DateTime.UtcNow, DateTime.UtcNow.AddDays(14), TenantSubscriptionStatus.Trialing, currency);
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
    public async Task Should_Filter_By_TenantId()
    {
        var (tenantA, _) = await CreateTenantWithSubscriptionAsync();
        var (tenantB, _) = await CreateTenantWithSubscriptionAsync();

        var list = await WithUnitOfWorkAsync(() => _adminSubscriptionAppService.GetListAsync(new AdminSubscriptionFilterDto { TenantId = tenantA }));

        list.Items.ShouldHaveSingleItem();
        list.Items.Single().TenantId.ShouldBe(tenantA);
        list.Items.Select(s => s.TenantId).ShouldNotContain(tenantB);
    }

    // Regression test for a real bug: SetPlanNamesAsync (used by GetListAsync) only ever resolved
    // PlanId -> PlanName, never PendingPlanId -> PendingPlanName — so the Subscriptions list always
    // showed a pending request as "Growth -> Requested:" with no target plan name, even though the
    // single-row responses from Approve/RejectPlanChangeAsync (and the notification fired at request
    // time) always had the real name. Caught live via a screenshot of the Admin Portal.
    [Fact]
    public async Task GetListAsync_Should_Resolve_The_Pending_Plan_Name_For_A_Requested_Change()
    {
        var (tenantId, subscriptionId) = await CreateTenantWithSubscriptionAsync();

        Guid newPlanId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var scalePlan = SubscriptionPlan.Create(Guid.NewGuid(), "Scale", 149m, 20m, "{}");
                await _planRepository.InsertAsync(scalePlan, autoSave: true);
                newPlanId = scalePlan.Id;
            }
        });
        await RequestPlanChangeAsync(tenantId, subscriptionId, newPlanId);

        var list = await WithUnitOfWorkAsync(() => _adminSubscriptionAppService.GetListAsync(new AdminSubscriptionFilterDto { TenantId = tenantId }));

        var dto = list.Items.Single();
        dto.PlanName.ShouldBe("Growth");
        dto.PendingPlanId.ShouldBe(newPlanId);
        dto.PendingPlanName.ShouldBe("Scale");
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
                var invoice = Invoice.Create(Guid.NewGuid(), subscriptionId, 49m, Currency.Syp, DateTime.UtcNow);
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

    [Fact]
    public async Task GetPaymentsAsync_Should_Return_The_Payment_Recorded_For_An_Invoice()
    {
        var (tenantId, subscriptionId) = await CreateTenantWithSubscriptionAsync();

        Guid invoiceId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var invoice = Invoice.Create(Guid.NewGuid(), subscriptionId, 49m, Currency.Syp, DateTime.UtcNow);
                await _invoiceRepository.InsertAsync(invoice, autoSave: true);
                invoiceId = invoice.Id;
            }
        });

        await WithUnitOfWorkAsync(() => _adminSubscriptionAppService.RecordManualPaymentAsync(new RecordManualPaymentDto
        {
            InvoiceId = invoiceId,
            ProviderTransactionRef = "wire-12345"
        }));

        var payments = await WithUnitOfWorkAsync(() => _adminSubscriptionAppService.GetPaymentsAsync(new AdminPaymentFilterDto { InvoiceId = invoiceId }));

        payments.Items.ShouldHaveSingleItem();
        var payment = payments.Items.Single();
        payment.InvoiceId.ShouldBe(invoiceId);
        payment.Provider.ShouldBe("Manual");
        payment.ProviderTransactionRef.ShouldBe("wire-12345");
        payment.Status.ShouldBe(PaymentStatus.Succeeded);
    }

    [Fact]
    public async Task GetPaymentsAsync_Should_Not_Return_Payments_For_A_Different_Invoice()
    {
        var (tenantId, subscriptionId) = await CreateTenantWithSubscriptionAsync();

        Guid invoiceAId = default, invoiceBId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var invoiceA = Invoice.Create(Guid.NewGuid(), subscriptionId, 49m, Currency.Syp, DateTime.UtcNow);
                await _invoiceRepository.InsertAsync(invoiceA, autoSave: true);
                invoiceAId = invoiceA.Id;

                var invoiceB = Invoice.Create(Guid.NewGuid(), subscriptionId, 49m, Currency.Syp, DateTime.UtcNow);
                await _invoiceRepository.InsertAsync(invoiceB, autoSave: true);
                invoiceBId = invoiceB.Id;
            }
        });

        await WithUnitOfWorkAsync(() => _adminSubscriptionAppService.RecordManualPaymentAsync(new RecordManualPaymentDto { InvoiceId = invoiceAId }));

        var payments = await WithUnitOfWorkAsync(() => _adminSubscriptionAppService.GetPaymentsAsync(new AdminPaymentFilterDto { InvoiceId = invoiceBId }));

        payments.Items.ShouldBeEmpty();
    }

    // Mirrors what Billing.BillingAppService.ChangePlanAsync itself does (RequestPlanChange, not
    // ChangePlan) — calling the domain method directly here rather than going through
    // IBillingAppService, since that service isn't otherwise a dependency of this test class.
    private async Task<Guid> RequestPlanChangeAsync(Guid tenantId, Guid subscriptionId, Guid newPlanId)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var subscription = await _subscriptionRepository.GetAsync(subscriptionId);
                subscription.RequestPlanChange(newPlanId, DateTime.UtcNow);
                await _subscriptionRepository.UpdateAsync(subscription, autoSave: true);
            }
        });
        return newPlanId;
    }

    [Fact]
    public async Task ApprovePlanChangeAsync_Should_Notify_The_Tenant()
    {
        var (tenantId, subscriptionId) = await CreateTenantWithSubscriptionAsync();
        var staffId = await CreateTenantStaffAsync(tenantId);

        Guid newPlanId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var scalePlan = SubscriptionPlan.Create(Guid.NewGuid(), "Scale", 149m, 20m, "{}");
                await _planRepository.InsertAsync(scalePlan, autoSave: true);
                newPlanId = scalePlan.Id;
            }
        });
        await RequestPlanChangeAsync(tenantId, subscriptionId, newPlanId);

        await WithUnitOfWorkAsync(() => _adminSubscriptionAppService.ApprovePlanChangeAsync(subscriptionId));

        using (_currentTenant.Change(tenantId))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var notified = await _userNotificationRepository.GetListAsync(n => n.UserId == staffId);
                notified.ShouldNotBeEmpty();
            });
        }
    }

    [Fact]
    public async Task RejectPlanChangeAsync_Should_Notify_The_Tenant()
    {
        var (tenantId, subscriptionId) = await CreateTenantWithSubscriptionAsync();
        var staffId = await CreateTenantStaffAsync(tenantId);

        Guid newPlanId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var scalePlan = SubscriptionPlan.Create(Guid.NewGuid(), "Scale", 149m, 20m, "{}");
                await _planRepository.InsertAsync(scalePlan, autoSave: true);
                newPlanId = scalePlan.Id;
            }
        });
        await RequestPlanChangeAsync(tenantId, subscriptionId, newPlanId);

        await WithUnitOfWorkAsync(() => _adminSubscriptionAppService.RejectPlanChangeAsync(subscriptionId));

        using (_currentTenant.Change(tenantId))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var notified = await _userNotificationRepository.GetListAsync(n => n.UserId == staffId);
                notified.ShouldNotBeEmpty();
            });
        }
    }

    [Fact]
    public async Task ApprovePlanChangeAsync_Should_Apply_The_Pending_Plan_And_Push_Its_Features_And_Activate()
    {
        var (tenantId, subscriptionId) = await CreateTenantWithSubscriptionAsync();

        Guid newPlanId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var scalePlan = SubscriptionPlan.Create(Guid.NewGuid(), "Scale", 149m, 20m, $"{{\"{EksabliFeatures.MaxBranches}\":\"25\"}}");
                await _planRepository.InsertAsync(scalePlan, autoSave: true);
                newPlanId = scalePlan.Id;
            }
        });
        await RequestPlanChangeAsync(tenantId, subscriptionId, newPlanId);

        var dto = await WithUnitOfWorkAsync(() => _adminSubscriptionAppService.ApprovePlanChangeAsync(subscriptionId));

        dto.PlanId.ShouldBe(newPlanId);
        dto.PlanName.ShouldBe("Scale");
        dto.PendingPlanId.ShouldBeNull();
        dto.Status.ShouldBe(TenantSubscriptionStatus.Active);

        using (_currentTenant.Change(tenantId))
        {
            var maxBranches = await _featureChecker.GetAsync<int>(EksabliFeatures.MaxBranches);
            maxBranches.ShouldBe(25);
        }
    }

    [Fact]
    public async Task RejectPlanChangeAsync_Should_Clear_The_Request_Without_Changing_The_Plan_Or_Status()
    {
        var (tenantId, subscriptionId) = await CreateTenantWithSubscriptionAsync();

        Guid originalPlanId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                originalPlanId = (await _subscriptionRepository.GetAsync(subscriptionId)).PlanId;
            }
        });

        Guid newPlanId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var scalePlan = SubscriptionPlan.Create(Guid.NewGuid(), "Scale", 149m, 20m, "{}");
                await _planRepository.InsertAsync(scalePlan, autoSave: true);
                newPlanId = scalePlan.Id;
            }
        });
        await RequestPlanChangeAsync(tenantId, subscriptionId, newPlanId);

        var dto = await WithUnitOfWorkAsync(() => _adminSubscriptionAppService.RejectPlanChangeAsync(subscriptionId));

        dto.PlanId.ShouldBe(originalPlanId);
        dto.PendingPlanId.ShouldBeNull();
        dto.Status.ShouldBe(TenantSubscriptionStatus.Trialing); // untouched, not activated by a rejection
    }

    [Fact]
    public async Task ApprovePlanChangeAsync_Should_Throw_When_Nothing_Is_Pending()
    {
        var (_, subscriptionId) = await CreateTenantWithSubscriptionAsync();

        await Assert.ThrowsAsync<UserFriendlyException>(() =>
            WithUnitOfWorkAsync(() => _adminSubscriptionAppService.ApprovePlanChangeAsync(subscriptionId)));
    }

    [Fact]
    public async Task RejectPlanChangeAsync_Should_Throw_When_Nothing_Is_Pending()
    {
        var (_, subscriptionId) = await CreateTenantWithSubscriptionAsync();

        await Assert.ThrowsAsync<UserFriendlyException>(() =>
            WithUnitOfWorkAsync(() => _adminSubscriptionAppService.RejectPlanChangeAsync(subscriptionId)));
    }

    [Fact]
    public async Task GetStatsAsync_Should_Return_Separate_Mrr_Per_Currency()
    {
        var (sypTenantId, sypSubscriptionId) = await CreateTenantWithSubscriptionAsync(Currency.Syp);
        var (usdTenantId, usdSubscriptionId) = await CreateTenantWithSubscriptionAsync(Currency.Usd);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(sypTenantId))
            {
                var subscription = await _subscriptionRepository.GetAsync(sypSubscriptionId);
                subscription.MarkActive();
                await _subscriptionRepository.UpdateAsync(subscription, autoSave: true);
            }

            using (_currentTenant.Change(usdTenantId))
            {
                var subscription = await _subscriptionRepository.GetAsync(usdSubscriptionId);
                subscription.MarkActive();
                await _subscriptionRepository.UpdateAsync(subscription, autoSave: true);
            }
        });

        // CreateTenantWithSubscriptionAsync seeds a fresh "Growth" plan (49 SYP / 5 USD) per tenant —
        // one active subscriber each, so ApproxMrrByCurrency must show 49 SYP and 5 USD, never a single
        // combined 54.
        var stats = await WithUnitOfWorkAsync(() => _adminSubscriptionAppService.GetStatsAsync());

        stats.ApproxMrrByCurrency.Count.ShouldBe(2);
        stats.ApproxMrrByCurrency.Single(a => a.Currency == Currency.Syp).Amount.ShouldBe(49m);
        stats.ApproxMrrByCurrency.Single(a => a.Currency == Currency.Usd).Amount.ShouldBe(5m);
    }

    [Fact]
    public async Task GetMrrTrendAsync_Should_Not_Mix_Currencies_In_The_Same_Month()
    {
        var (tenantId, subscriptionId) = await CreateTenantWithSubscriptionAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var sypInvoice = Invoice.Create(Guid.NewGuid(), subscriptionId, 49m, Currency.Syp, DateTime.UtcNow);
                sypInvoice.MarkPaid(DateTime.UtcNow);
                await _invoiceRepository.InsertAsync(sypInvoice, autoSave: true);

                var usdInvoice = Invoice.Create(Guid.NewGuid(), subscriptionId, 5m, Currency.Usd, DateTime.UtcNow);
                usdInvoice.MarkPaid(DateTime.UtcNow);
                await _invoiceRepository.InsertAsync(usdInvoice, autoSave: true);
            }
        });

        var trend = await WithUnitOfWorkAsync(() => _adminSubscriptionAppService.GetMrrTrendAsync());

        var thisMonth = trend.Single(m => m.Year == DateTime.UtcNow.Year && m.Month == DateTime.UtcNow.Month);
        thisMonth.AmountsByCurrency.Count.ShouldBe(2);
        thisMonth.AmountsByCurrency.Single(a => a.Currency == Currency.Syp).Amount.ShouldBe(49m);
        thisMonth.AmountsByCurrency.Single(a => a.Currency == Currency.Usd).Amount.ShouldBe(5m);
    }
}
