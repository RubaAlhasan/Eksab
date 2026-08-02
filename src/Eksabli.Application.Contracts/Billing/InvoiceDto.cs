using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Billing;

public class InvoiceDto : AuditedEntityDto<Guid>
{
    public Guid TenantSubscriptionId { get; set; }

    public decimal Amount { get; set; }

    public InvoiceStatus Status { get; set; }

    public DateTime DueDate { get; set; }

    public DateTime? PaidAt { get; set; }
}
