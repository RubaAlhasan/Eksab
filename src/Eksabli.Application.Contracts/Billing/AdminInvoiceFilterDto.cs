using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Billing;

public class AdminInvoiceFilterDto : PagedAndSortedResultRequestDto
{
    public InvoiceStatus? Status { get; set; }

    public Guid? TenantSubscriptionId { get; set; }
}
