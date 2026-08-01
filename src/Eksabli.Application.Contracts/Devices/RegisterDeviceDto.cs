using System.ComponentModel.DataAnnotations;

namespace Eksabli.Devices;

public class RegisterDeviceDto
{
    [Required]
    public DevicePlatform Platform { get; set; }

    [Required]
    [StringLength(DeviceConsts.MaxPushTokenLength)]
    public string PushToken { get; set; } = string.Empty;

    [StringLength(DeviceConsts.MaxAppVersionLength)]
    public string? AppVersion { get; set; }
}
