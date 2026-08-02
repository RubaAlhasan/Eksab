using System;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.Billing;

public class RecordManualPaymentDto
{
    [Required]
    public Guid InvoiceId { get; set; }

    [StringLength(PaymentConsts.MaxProviderTransactionRefLength)]
    public string? ProviderTransactionRef { get; set; }
}
