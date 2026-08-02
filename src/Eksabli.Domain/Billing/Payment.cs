using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Billing;

// No standalone repository — reached through Invoice, per the DB-design cross-cutting note
// ("child entities reached through their aggregate root").
public class Payment : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid InvoiceId { get; private set; }

    // Free text, not an enum — gateways are pluggable ("Null" today, "Stripe"/"Paddle" later).
    public string Provider { get; private set; }

    public string? ProviderTransactionRef { get; private set; }

    public PaymentStatus Status { get; private set; }

    protected Payment()
    {
        Provider = string.Empty;
    }

    private Payment(Guid id, Guid invoiceId, string provider)
        : base(id)
    {
        InvoiceId = invoiceId;
        Provider = Check.NotNullOrWhiteSpace(provider, nameof(provider), PaymentConsts.MaxProviderLength);
        Status = PaymentStatus.Pending;
    }

    public static Payment Create(Guid id, Guid invoiceId, string provider)
    {
        return new Payment(id, invoiceId, provider);
    }

    public void MarkSucceeded(string? providerTransactionRef)
    {
        Status = PaymentStatus.Succeeded;
        ProviderTransactionRef = providerTransactionRef;
    }

    public void MarkFailed() => Status = PaymentStatus.Failed;
}
