using System;
using Eksabli.Billing;
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
        var subscription = TenantSubscription.Create(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(14), TenantSubscriptionStatus.Trialing);

        subscription.MarkPastDue();

        subscription.Status.ShouldBe(TenantSubscriptionStatus.PastDue);
    }

    [Fact]
    public void MarkActive_Should_Transition_Status()
    {
        var subscription = TenantSubscription.Create(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(14), TenantSubscriptionStatus.Trialing);

        subscription.MarkActive();

        subscription.Status.ShouldBe(TenantSubscriptionStatus.Active);
    }

    [Fact]
    public void Renew_Should_Update_RenewalDate()
    {
        var subscription = TenantSubscription.Create(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow, TenantSubscriptionStatus.Active);
        var newDate = DateTime.UtcNow.AddMonths(1);

        subscription.Renew(newDate);

        subscription.RenewalDate.ShouldBe(newDate);
    }

    [Fact]
    public void Cancel_Should_Transition_Status()
    {
        var subscription = TenantSubscription.Create(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow, TenantSubscriptionStatus.Active);

        subscription.Cancel();

        subscription.Status.ShouldBe(TenantSubscriptionStatus.Cancelled);
    }
}

public class InvoiceTests
{
    [Fact]
    public void MarkPaid_Should_Set_Status_And_PaidAt()
    {
        var invoice = Invoice.Create(Guid.NewGuid(), Guid.NewGuid(), 49m, DateTime.UtcNow);
        var paidAt = DateTime.UtcNow;

        invoice.MarkPaid(paidAt);

        invoice.Status.ShouldBe(InvoiceStatus.Paid);
        invoice.PaidAt.ShouldBe(paidAt);
    }

    [Fact]
    public void MarkOverdue_Should_Transition_Status()
    {
        var invoice = Invoice.Create(Guid.NewGuid(), Guid.NewGuid(), 49m, DateTime.UtcNow);

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
