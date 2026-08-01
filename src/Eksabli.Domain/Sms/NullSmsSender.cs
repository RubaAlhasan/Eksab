using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.DependencyInjection;

namespace Eksabli.Sms;

// Placeholder SMS sender — no real SMS provider has been chosen yet (open question, see
// docs/eksabli-loyalty-platform/features/05-campaigns-notifications/README.md). Just logs the
// message so OTP codes remain usable in dev/testing without a real delivery channel.
public class NullSmsSender : ISmsSender, ITransientDependency
{
    public ILogger<NullSmsSender> Logger { get; set; } = NullLogger<NullSmsSender>.Instance;

    public Task SendAsync(string phoneNumber, string message)
    {
        Logger.LogWarning(
            "[DEV SMS PLACEHOLDER — no real SMS provider configured yet] To: {PhoneNumber} | {Message}",
            phoneNumber, message);
        return Task.CompletedTask;
    }
}
