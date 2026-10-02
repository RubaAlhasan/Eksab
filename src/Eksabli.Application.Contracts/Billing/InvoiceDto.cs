using System;
using Volo.Abp.Application.Dtos;
using Eksabli.Shared;

namespace Eksabli.Billing;

public class InvoiceDto : AuditedEntityDto<Guid>
{
    public Guid TenantSubscriptionId { get; set; }

    public decimal Amount { get; set; }

    public Currency Currency { get; set; }

    public InvoiceStatus Status { get; set; }

    public DateTime DueDate { get; set; }

    public DateTime? PaidAt { get; set; }
}
