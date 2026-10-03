using System;
using Eksabli.Billing;
using Eksabli.Shared;
using Shouldly;
using Xunit;

namespace Eksabli.Billing;

// Pure entity-behavior tests — no DB/DI needed, mirrors how PointsWallet/Coupon state transitions
// are exercised directly via Create(...) + behavior methods.
public class TenantSubscriptionTests
{
    [Fact]
    public void MarkPastDue_Should_Transition_Status()
    {
        var subscription = TenantSubscription.Create(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(14), TenantSubscriptionStatus.Trialing, Currency.Syp);

        subscription.MarkPastDue();

        subscription.Status.ShouldBe(TenantSubscriptionStatus.PastDue);
    }

    [Fact]
    public void MarkActive_Should_Transition_Status()
    {
        var subscription = TenantSubscription.Create(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(14), TenantSubscriptionStatus.Trialing, Currency.Syp);

        subscription.MarkActive();

        subscription.Status.ShouldBe(TenantSubscriptionStatus.Active);
    }

    [Fact]
    public void Renew_Should_Update_RenewalDate()
    {
        var subscription = TenantSubscription.Create(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow, TenantSubscriptionStatus.Active, Currency.Syp);
        var newDate = DateTime.UtcNow.AddMonths(1);

        subscription.Renew(newDate);

        subscription.RenewalDate.ShouldBe(newDate);
    }

    [Fact]
    public void Cancel_Should_Transition_Status()
    {
        var subscription = TenantSubscription.Create(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow, TenantSubscriptionStatus.Active, Currency.Syp);

        subscription.Cancel();

        subscription.Status.ShouldBe(TenantSubscriptionStatus.Cancelled);
    }
}

public class InvoiceTests
{
    [Fact]
    public void MarkPaid_Should_Set_Status_And_PaidAt()
    {
        var invoice = Invoice.Create(Guid.NewGuid(), Guid.NewGuid(), 49m, Currency.Syp, DateTime.UtcNow);
        var paidAt = DateTime.UtcNow;

        invoice.MarkPaid(paidAt);

        invoice.Status.ShouldBe(InvoiceStatus.Paid);
        invoice.PaidAt.ShouldBe(paidAt);
    }

    [Fact]
    public void MarkOverdue_Should_Transition_Status()
    {
        var invoice = Invoice.Create(Guid.NewGuid(), Guid.NewGuid(), 49m, Currency.Syp, DateTime.UtcNow);

        invoice.MarkOverdue();

        invoice.Status.ShouldBe(InvoiceStatus.Overdue);
    }
}

public class PaymentTests
{
    [Fact]
    public void MarkSucceeded_Should_Set_Status_And_ProviderTransactionRef()
    {
        var payment = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), "Null");

        payment.MarkSucceeded("NULL-abc123");

        payment.Status.ShouldBe(PaymentStatus.Succeeded);
        payment.ProviderTransactionRef.ShouldBe("NULL-abc123");
    }

    [Fact]
    public void MarkFailed_Should_Transition_Status()
    {
        var payment = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), "Null");

        payment.MarkFailed();

        payment.Status.ShouldBe(PaymentStatus.Failed);
    }
}

public class SubscriptionPlanTests
{
    [Fact]
    public void GetMonthlyPrice_Should_Return_The_Price_Matching_The_Given_Currency()
    {
        var plan = SubscriptionPlan.Create(Guid.NewGuid(), "Growth", 49m, 5m, "{}", isTrialDefault: true);

        plan.GetMonthlyPrice(Currency.Syp).ShouldBe(49m);
        plan.GetMonthlyPrice(Currency.Usd).ShouldBe(5m);
    }

    [Fact]
    public void SetMonthlyPriceSyp_And_SetMonthlyPriceUsd_Should_Update_Independently()
    {
        var plan = SubscriptionPlan.Create(Guid.NewGuid(), "Growth", 49m, 5m, "{}");

        plan.SetMonthlyPriceSyp(59m);
        plan.SetMonthlyPriceUsd(6m);

        plan.MonthlyPriceSyp.ShouldBe(59m);
        plan.MonthlyPriceUsd.ShouldBe(6m);
    }
}
