using System;
using System.Threading.Tasks;

namespace Eksabli.Billing;

public interface IPaymentGateway
{
    Task<PaymentGatewayResult> ChargeAsync(Guid tenantId, decimal amount, string description);
}

public class PaymentGatewayResult
{
    public bool Succeeded { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string? ProviderTransactionRef { get; set; }
}
