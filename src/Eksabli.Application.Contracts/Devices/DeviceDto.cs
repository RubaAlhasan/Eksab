using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Devices;

public class DeviceDto : AuditedEntityDto<Guid>
{
    public Guid CustomerId { get; set; }

    public DevicePlatform Platform { get; set; }

    public string? PushToken { get; set; }

    public DateTime LastActiveAt { get; set; }

    public string? AppVersion { get; set; }
}
