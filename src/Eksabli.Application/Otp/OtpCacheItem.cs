using System;

namespace Eksabli.Otp;

[Serializable]
public class OtpCacheItem
{
    public string Code { get; set; } = string.Empty;
}
