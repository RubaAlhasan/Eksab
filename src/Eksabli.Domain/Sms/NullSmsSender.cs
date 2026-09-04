using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Sms;

// Placeholder SMS sender — no real SMS provider has been chosen yet (open question, see
// docs/eksabli-loyalty-platform/features/05-campaigns-notifications/README.md). Logs the message (as
// before) AND persists it to SmsLog so OTP verification codes are browsable from Admin Portal >
// Verification Codes instead of only grep-able from the log file — see SmsLog's own comment for why
// this lives here specifically (it naturally stops mattering once a real ISmsSender replaces this one).
public class NullSmsSender : ISmsSender, ITransientDependency
{
    private readonly IRepository<SmsLog, Guid> _smsLogRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;

    public ILogger<NullSmsSender> Logger { get; set; } = NullLogger<NullSmsSender>.Instance;

    public NullSmsSender(
        IRepository<SmsLog, Guid> smsLogRepository,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant)
    {
        _smsLogRepository = smsLogRepository;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
    }

    public async Task SendAsync(string phoneNumber, string message)
    {
        Logger.LogWarning(
            "[DEV SMS PLACEHOLDER — no real SMS provider configured yet] To: {PhoneNumber} | {Message}",
            phoneNumber, message);

        // SmsLog is Host-realm (no IMultiTenant) — this can be called from inside a tenant-scoped
        // context (e.g. a business's own campaign send), same "switch to null before writing a
        // Host-realm row" shape as OtpAppService.RegisterAsync.
        using (_currentTenant.Change(null))
        {
            await _smsLogRepository.InsertAsync(
                SmsLog.Create(
                    _guidGenerator.Create(),
                    Truncate(phoneNumber, SmsLogConsts.MaxPhoneNumberLength),
                    Truncate(message, SmsLogConsts.MaxMessageLength)),
                autoSave: true);
        }
    }

    // Defensive only — callers pass user-typed (OTP) or template-composed (campaign) text that isn't
    // otherwise validated against SmsLog's own column lengths before it gets here.
    private static string Truncate(string value, int maxLength)
    {
        return value.Length > maxLength ? value[..maxLength] : value;
    }
}
