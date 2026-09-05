using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace Eksabli.Sms;

// Dev/testing convenience — persists every message NullSmsSender would otherwise only write to the
// log file, so OTP verification codes (and any other SMS text, e.g. campaign sends going through the
// same ISmsSender) are browsable from the Admin Portal instead of requiring server log access. Written
// ONLY by NullSmsSender (see its own comment) — once a real SMS provider is wired in and NullSmsSender
// is retired the same way ConfigureFcm swaps in FirebaseCloudMessagingSender, this table simply stops
// growing; nothing else needs to change.
//
// Host-realm, no IMultiTenant — matches ISmsSender.SendAsync's own tenant-agnostic shape (it's called
// with just "phone number + message", the same call OTP (Otp.OtpAppService) and campaign notifications
// (Notifications.NotificationSender) both go through) — a row here doesn't know or care which tenant
// (if any) triggered it.
public class SmsLog : AuditedAggregateRoot<Guid>
{
    public string PhoneNumber { get; private set; }

    public string Message { get; private set; }

    protected SmsLog()
    {
        /* Required by the ORM */
        PhoneNumber = string.Empty;
        Message = string.Empty;
    }

    private SmsLog(Guid id, string phoneNumber, string message)
        : base(id)
    {
        PhoneNumber = phoneNumber;
        Message = message;
    }

    public static SmsLog Create(Guid id, string phoneNumber, string message)
    {
        return new SmsLog(id, phoneNumber, message);
    }
}
