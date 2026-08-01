using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace Eksabli.Devices;

public class Device : AuditedAggregateRoot<Guid>
{
    // Host-realm entity — no IMultiTenant. Soft reference, same identity space as Membership.CustomerId
    // (i.e. the Host-realm IdentityUser.Id, not CustomerProfile.Id).
    public Guid CustomerId { get; private set; }

    public DevicePlatform Platform { get; private set; }

    public string? PushToken { get; private set; }

    public DateTime LastActiveAt { get; private set; }

    public string? AppVersion { get; private set; }

    protected Device()
    {
        /* Required by the ORM */
    }

    private Device(Guid id, Guid customerId, DevicePlatform platform)
        : base(id)
    {
        CustomerId = customerId;
        Platform = platform;
        LastActiveAt = DateTime.UtcNow;
    }

    public static Device Create(Guid id, Guid customerId, DevicePlatform platform)
    {
        return new Device(id, customerId, platform);
    }

    public void UpdatePushToken(string? pushToken) => PushToken = pushToken;

    public void Touch(string? appVersion = null)
    {
        LastActiveAt = DateTime.UtcNow;
        if (appVersion != null)
        {
            AppVersion = appVersion;
        }
    }
}
