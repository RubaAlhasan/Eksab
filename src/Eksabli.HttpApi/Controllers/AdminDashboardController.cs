using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Dashboards;
using Eksabli.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/admin-dashboard")]
[Authorize(EksabliPermissions.PlatformReports.Default)]
public class AdminDashboardController : EksabliController
{
    private readonly IAdminDashboardAppService _adminDashboardAppService;

    public AdminDashboardController(IAdminDashboardAppService adminDashboardAppService)
    {
        _adminDashboardAppService = adminDashboardAppService;
    }

    [HttpGet("summary")]
    public Task<AdminDashboardSummaryDto> GetSummaryAsync([FromQuery] DashboardRangeDto input)
    {
        return _adminDashboardAppService.GetSummaryAsync(input);
    }

    [HttpGet("trends")]
    public Task<List<AdminDashboardTrendPointDto>> GetTrendsAsync([FromQuery] DashboardRangeDto input)
    {
        return _adminDashboardAppService.GetTrendsAsync(input);
    }

    [HttpGet("top-businesses")]
    public Task<List<TopBusinessDto>> GetTopBusinessesAsync([FromQuery] DashboardRangeDto input)
    {
        return _adminDashboardAppService.GetTopBusinessesAsync(input);
    }

    [HttpGet("top-offers")]
    public Task<List<TopOfferDto>> GetTopOffersAsync([FromQuery] DashboardRangeDto input)
    {
        return _adminDashboardAppService.GetTopOffersAsync(input);
    }

    [HttpGet("alerts")]
    public Task<AdminAlertsDto> GetAlertsAsync()
    {
        return _adminDashboardAppService.GetAlertsAsync();
    }

    [HttpGet("activity")]
    public Task<List<AdminActivityItemDto>> GetActivityAsync()
    {
        return _adminDashboardAppService.GetActivityAsync();
    }
}
