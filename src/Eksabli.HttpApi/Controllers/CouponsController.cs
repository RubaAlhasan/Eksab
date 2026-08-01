using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Rewards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/coupon")]
[Authorize]
public class CouponsController : EksabliController
{
    private readonly ICouponAppService _couponAppService;

    public CouponsController(ICouponAppService couponAppService)
    {
        _couponAppService = couponAppService;
    }

    [HttpGet("catalog/{tenantId}")]
    public Task<PagedResultDto<RewardDto>> GetCatalogAsync(Guid tenantId, [FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _couponAppService.GetCatalogAsync(tenantId, input);
    }

    [HttpPost("redeem")]
    public Task<CouponDto> RedeemAsync(RedeemRewardDto input)
    {
        return _couponAppService.RedeemAsync(input);
    }

    [HttpGet("my")]
    public Task<List<CouponDto>> GetMyCouponsAsync([FromQuery] Guid? tenantId = null)
    {
        return _couponAppService.GetMyCouponsAsync(tenantId);
    }
}
