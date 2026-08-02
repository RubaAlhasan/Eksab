using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Engagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/referral")]
[Authorize]
public class ReferralsController : EksabliController
{
    private readonly IReferralAppService _referralAppService;

    public ReferralsController(IReferralAppService referralAppService)
    {
        _referralAppService = referralAppService;
    }

    [HttpGet("my-code")]
    public Task<ReferralCodeDto> GetMyReferralCodeAsync([FromQuery] Guid tenantId)
    {
        return _referralAppService.GetMyReferralCodeAsync(tenantId);
    }

    [HttpGet("my")]
    public Task<List<ReferralDto>> GetMyReferralsAsync()
    {
        return _referralAppService.GetMyReferralsAsync();
    }
}
