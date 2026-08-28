using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Billing;

public class AdminPaymentFilterDto : PagedAndSortedResultRequestDto
{
    public Guid? InvoiceId { get; set; }

    public PaymentStatus? Status { get; set; }
}
