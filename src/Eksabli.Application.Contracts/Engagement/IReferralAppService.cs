using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace Eksabli.Engagement;

// Host-realm, customer-scoped — exposed via an explicit controller
// (src/Eksabli.HttpApi/Controllers/ReferralsController.cs).
[RemoteService(IsEnabled = false)]
public interface IReferralAppService : IApplicationService
{
    // 404s if the caller isn't a member of tenantId yet — only existing members can refer others.
    Task<ReferralCodeDto> GetMyReferralCodeAsync(Guid tenantId);

    // "Referral status list" — as referrer, across every business the caller has referred someone into.
    Task<List<ReferralDto>> GetMyReferralsAsync();
}
