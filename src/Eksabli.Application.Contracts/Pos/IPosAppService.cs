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

    // Read-only — computes the exact same breakdown AwardPointsByCustomerIdAsync would use if called
    // right now, without writing a PointsTransaction or touching the wallet. Only meaningful for the
    // Phone Lookup identify mode, where a customer is known before the sale amount is entered; there's
    // no equivalent for QR (the token is burned, and the award made, on the very first read).
    Task<PointsPreviewDto> PreviewPointsAsync(Guid customerId, PreviewPointsDto input);

    Task<AwardPointsResultDto> ManualAdjustAsync(ManualAdjustDto input);

    // Read-only preview of a scanned/typed code. Deliberately separate from ConfirmRedemptionAsync:
    // staff review who and what before committing, and a lookup must never move points.
    Task<RedemptionLookupDto> LookupRedemptionAsync(LookupRedemptionDto input);

    Task<RedemptionConfirmationDto> ConfirmRedemptionAsync(ConfirmRedemptionDto input);

    Task<RedemptionRejectionDto> RejectRedemptionAsync(RejectRedemptionDto input);
}
