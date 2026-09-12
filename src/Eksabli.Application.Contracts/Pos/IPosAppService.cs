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

    // Read-only preview of a scanned/typed code. Deliberately separate from ConfirmRedemptionAsync:
    // staff review who and what before committing, and a lookup must never move points.
    Task<RedemptionLookupDto> LookupRedemptionAsync(LookupRedemptionDto input);

    Task<RedemptionConfirmationDto> ConfirmRedemptionAsync(ConfirmRedemptionDto input);

    Task<RedemptionRejectionDto> RejectRedemptionAsync(RejectRedemptionDto input);
}
