using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Permissions;
using Eksabli.Reports;
using Eksabli.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Content;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/report")]
[Authorize(EksabliPermissions.Reports.Default)]
public class ReportsController : EksabliController
{
    private readonly IReportsAppService _reportsAppService;

    public ReportsController(IReportsAppService reportsAppService)
    {
        _reportsAppService = reportsAppService;
    }

    [HttpGet("dashboard-home")]
    public Task<DashboardHomeDto> GetDashboardHomeAsync()
    {
        return _reportsAppService.GetDashboardHomeAsync();
    }

    [HttpGet("member-growth")]
    public Task<List<MemberGrowthPointDto>> GetMemberGrowthAsync([FromQuery] ReportPeriodDto input)
    {
        return _reportsAppService.GetMemberGrowthAsync(input);
    }

    [HttpGet("redemption-rate")]
    public Task<RedemptionRateReportDto> GetRedemptionRateAsync([FromQuery] ReportPeriodDto input)
    {
        return _reportsAppService.GetRedemptionRateAsync(input);
    }

    [HttpGet("branch-comparison")]
    public Task<List<BranchComparisonDto>> GetBranchComparisonAsync([FromQuery] ReportPeriodDto input)
    {
        return _reportsAppService.GetBranchComparisonAsync(input);
    }

    [HttpGet("customer-segments")]
    public Task<CustomerSegmentReportDto> GetCustomerSegmentsAsync()
    {
        return _reportsAppService.GetCustomerSegmentsAsync();
    }

    [HttpGet("tier-distribution")]
    public Task<List<TierDistributionDto>> GetTierDistributionAsync()
    {
        return _reportsAppService.GetTierDistributionAsync();
    }

    [HttpGet("top-customers")]
    public Task<List<TopCustomerDto>> GetTopCustomersAsync([FromQuery] int count = 10)
    {
        return _reportsAppService.GetTopCustomersAsync(count);
    }

    [HttpGet("campaign/{campaignId}/performance")]
    public Task<CampaignPerformanceDto> GetCampaignPerformanceAsync(Guid campaignId)
    {
        return _reportsAppService.GetCampaignPerformanceAsync(campaignId);
    }

    [HttpGet("notification-delivery-rates")]
    public Task<List<NotificationDeliveryRateDto>> GetNotificationDeliveryRatesAsync([FromQuery] ReportPeriodDto input)
    {
        return _reportsAppService.GetNotificationDeliveryRatesAsync(input);
    }

    [Authorize(EksabliPermissions.Reports.Export)]
    [HttpGet("transactions/download-token")]
    public Task<DownloadTokenResultDto> GetTransactionsDownloadTokenAsync()
    {
        return _reportsAppService.GetTransactionsDownloadTokenAsync();
    }

    [AllowAnonymous]
    [HttpGet("transactions/as-excel-file")]
    public Task<IRemoteStreamContent> GetTransactionsAsExcelFileAsync([FromQuery] TransactionsExcelDownloadDto input)
    {
        return _reportsAppService.GetTransactionsAsExcelFileAsync(input);
    }
}
