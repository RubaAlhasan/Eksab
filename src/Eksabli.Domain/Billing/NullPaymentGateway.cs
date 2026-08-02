using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.DependencyInjection;

namespace Eksabli.Billing;

// Placeholder payment gateway — no real payment provider has been chosen yet (open question, see
// docs/eksabli-loyalty-platform/features/04-billing-subscriptions/README.md). Always "succeeds" so
// subscription renewals stay usable in dev/testing without a real payment rail. Mirrors
// src/Eksabli.Domain/Sms/NullSmsSender.cs.
public class NullPaymentGateway : IPaymentGateway, ITransientDependency
{
    public ILogger<NullPaymentGateway> Logger { get; set; } = NullLogger<NullPaymentGateway>.Instance;

    public Task<PaymentGatewayResult> ChargeAsync(Guid tenantId, decimal amount, string description)
    {
        Logger.LogWarning(
            "[DEV PAYMENT PLACEHOLDER — no real payment provider configured yet] TenantId: {TenantId} | Amount: {Amount} | {Description}",
            tenantId, amount, description);

        return Task.FromResult(new PaymentGatewayResult
        {
            Succeeded = true,
            ProviderName = "Null",
            ProviderTransactionRef = "NULL-" + Guid.NewGuid().ToString("N")
        });
    }
}
