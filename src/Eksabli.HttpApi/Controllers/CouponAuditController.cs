using System.Threading.Tasks;
using Eksabli.Permissions;
using Eksabli.Rewards;
using Eksabli.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/coupon-audit")]
[Authorize(EksabliPermissions.Rewards.Default)]
public class CouponAuditController : EksabliController
{
    private readonly ICouponAuditAppService _couponAuditAppService;

    public CouponAuditController(ICouponAuditAppService couponAuditAppService)
    {
        _couponAuditAppService = couponAuditAppService;
    }

    [HttpGet]
    public Task<PagedResultDto<CouponDto>> GetListAsync([FromQuery] CouponAuditFilterDto input)
    {
        return _couponAuditAppService.GetListAsync(input);
    }

    [AllowAnonymous]
    [HttpGet("as-excel-file")]
    public Task<IRemoteStreamContent> GetListAsExcelFileAsync([FromQuery] CouponExcelDownloadDto input)
    {
        return _couponAuditAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet("download-token")]
    public Task<DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _couponAuditAppService.GetDownloadTokenAsync();
    }
}
