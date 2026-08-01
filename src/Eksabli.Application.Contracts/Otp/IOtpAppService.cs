using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace Eksabli.Otp;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/OtpController.cs).
[RemoteService(IsEnabled = false)]
public interface IOtpAppService : IApplicationService
{
    Task RequestOtpAsync(RequestOtpDto input);
}
