using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace Eksabli.Account;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/AccountStatusController.cs).
[RemoteService(IsEnabled = false)]
public interface IAccountStatusAppService : IApplicationService
{
    Task<bool> GetMustChangePasswordAsync();
}
