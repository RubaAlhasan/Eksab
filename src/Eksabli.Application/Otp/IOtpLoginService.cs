using System.Threading.Tasks;

namespace Eksabli.Otp;

public interface IOtpLoginService
{
    Task<OtpValidationResult> ValidateAndResolveUserAsync(string phoneNumber, string code);
}
