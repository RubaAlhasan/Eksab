using Volo.Abp.Identity;

namespace Eksabli.Otp;

public class OtpValidationResult
{
    public bool IsValid { get; set; }

    // "invalid_code" | "expired_code"
    public string? ErrorCode { get; set; }

    public IdentityUser? User { get; set; }

    public bool IsNewUser { get; set; }
}
