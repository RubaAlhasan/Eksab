using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Billing;

public class PaymentDto : AuditedEntityDto<Guid>
{
    public Guid InvoiceId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string? ProviderTransactionRef { get; set; }

    public PaymentStatus Status { get; set; }
}
