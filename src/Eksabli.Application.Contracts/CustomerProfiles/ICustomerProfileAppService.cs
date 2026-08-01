using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace Eksabli.CustomerProfiles;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/CustomerProfileController.cs).
[RemoteService(IsEnabled = false)]
public interface ICustomerProfileAppService : IApplicationService
{
    Task<CustomerProfileDto> GetMyProfileAsync();

    Task<CustomerProfileDto> UpdateMyProfileAsync(UpdateCustomerProfileDto input);
}
