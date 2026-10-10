using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Dashboards;
using Eksabli.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/business-dashboard")]
[Authorize(EksabliPermissions.Reports.Default)]
public class BusinessDashboardController : EksabliController
{
    private readonly IBusinessDashboardAppService _businessDashboardAppService;

    public BusinessDashboardController(IBusinessDashboardAppService businessDashboardAppService)
    {
        _businessDashboardAppService = businessDashboardAppService;
    }

    [HttpGet("summary")]
    public Task<BusinessDashboardSummaryDto> GetSummaryAsync([FromQuery] DashboardRangeDto input)
    {
        return _businessDashboardAppService.GetSummaryAsync(input);
    }

    [HttpGet("trends")]
    public Task<List<BusinessDashboardTrendPointDto>> GetTrendsAsync([FromQuery] DashboardRangeDto input)
    {
        return _businessDashboardAppService.GetTrendsAsync(input);
    }

    [HttpGet("peak-hours")]
    public Task<List<PeakHourCellDto>> GetPeakHoursAsync([FromQuery] DashboardRangeDto input)
    {
        return _businessDashboardAppService.GetPeakHoursAsync(input);
    }

    [HttpGet("offers")]
    public Task<List<BusinessOfferPerformanceDto>> GetOfferPerformanceAsync([FromQuery] DashboardRangeDto input)
    {
        return _businessDashboardAppService.GetOfferPerformanceAsync(input);
    }

    [HttpGet("insights")]
    public Task<List<BusinessInsightDto>> GetInsightsAsync()
    {
        return _businessDashboardAppService.GetInsightsAsync();
    }
}
