using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using Eksabli.Shared;

namespace Eksabli.Billing;

public class Invoice : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid TenantSubscriptionId { get; private set; }

    public decimal Amount { get; private set; }

    // Always the owning TenantSubscription's own Currency at creation time — see
    // TenantSubscription.Currency's own comment for why it never changes afterwards.
    public Currency Currency { get; private set; }

    public InvoiceStatus Status { get; private set; }

    public DateTime DueDate { get; private set; }

    public DateTime? PaidAt { get; private set; }

    protected Invoice()
    {
    }

    private Invoice(Guid id, Guid tenantSubscriptionId, decimal amount, Currency currency, DateTime dueDate)
        : base(id)
    {
        TenantSubscriptionId = tenantSubscriptionId;
        Amount = amount;
        Currency = currency;
        DueDate = dueDate;
        Status = InvoiceStatus.Draft;
    }

    public static Invoice Create(Guid id, Guid tenantSubscriptionId, decimal amount, Currency currency, DateTime dueDate)
    {
        return new Invoice(id, tenantSubscriptionId, amount, currency, dueDate);
    }

    public void MarkPaid(DateTime paidAt)
    {
        Status = InvoiceStatus.Paid;
        PaidAt = paidAt;
    }

    public void MarkOverdue() => Status = InvoiceStatus.Overdue;

    public void MarkSent() => Status = InvoiceStatus.Sent;
}
