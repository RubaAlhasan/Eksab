using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace Eksabli.Otp;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/OtpController.cs).
[RemoteService(IsEnabled = false)]
public interface IOtpAppService : IApplicationService
{
    Task RequestOtpAsync(RequestOtpDto input);

    // Not a login/token endpoint by itself — see RegisterCustomerDto's own comment. Call this, then
    // still finish sign-in the normal way via POST /connect/token (grant_type=otp).
    Task RegisterAsync(RegisterCustomerDto input);
}
