using System.Threading.Tasks;
using Eksabli.BusinessProfiles;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace Eksabli.Businesses;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/BusinessController.cs),
// not ABP's Auto API Controllers convention — excluded here to avoid a duplicate/conflicting route.
[RemoteService(IsEnabled = false)]
public interface IBusinessAppService : IApplicationService
{
    Task<BusinessRegistrationResultDto> RegisterAsync(RegisterBusinessDto input);

    Task<BusinessProfileDto> GetProfileAsync();

    Task<BusinessProfileDto> UpdateProfileAsync(UpdateBusinessProfileDto input);
}
