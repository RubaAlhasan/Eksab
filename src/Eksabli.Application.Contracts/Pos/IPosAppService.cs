using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace Eksabli.Pos;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/PosController.cs).
[RemoteService(IsEnabled = false)]
public interface IPosAppService : IApplicationService
{
    Task<CustomerLookupResultDto> LookupCustomerByPhoneAsync(PhoneLookupDto input);

    Task<AwardPointsResultDto> AwardPointsByQrAsync(AwardPointsByQrDto input);

    Task<AwardPointsResultDto> AwardPointsByCustomerIdAsync(Guid customerId, AwardPointsByCustomerIdDto input);

    Task<AwardPointsResultDto> ManualAdjustAsync(ManualAdjustDto input);
}
