using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Permissions;
using Eksabli.PlatformReports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/admin-platform-reports")]
[Authorize(EksabliPermissions.PlatformReports.Default)]
public class AdminPlatformReportsController : EksabliController
{
    private readonly IAdminPlatformReportAppService _adminPlatformReportAppService;

    public AdminPlatformReportsController(IAdminPlatformReportAppService adminPlatformReportAppService)
    {
        _adminPlatformReportAppService = adminPlatformReportAppService;
    }

    [HttpGet("tenant-growth")]
    public Task<List<TenantGrowthPointDto>> GetTenantGrowthAsync()
    {
        return _adminPlatformReportAppService.GetTenantGrowthAsync();
    }

    [HttpGet("ticket-metrics")]
    public Task<SupportTicketMetricsDto> GetTicketMetricsAsync()
    {
        return _adminPlatformReportAppService.GetTicketMetricsAsync();
    }
}
