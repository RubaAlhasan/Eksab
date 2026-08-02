using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace Eksabli.Reports;

// Centralizes KPI calculation so the definitions in
// docs/eksabli-loyalty-platform/features/07-business-dashboard/README.md#business-rules stay the
// single source of truth, not duplicated per chart. Exposed via an explicit controller
// (src/Eksabli.HttpApi/Controllers/ReportsController.cs). Tenant-scoped throughout — no new entities,
// this is a read layer over Membership/PointsTransaction/Campaign/Notification/Reward/Coupon/Tier
// from the other features.
[RemoteService(IsEnabled = false)]
public interface IReportsAppService : IApplicationService
{
    Task<DashboardHomeDto> GetDashboardHomeAsync();

    Task<List<MemberGrowthPointDto>> GetMemberGrowthAsync(ReportPeriodDto input);

    Task<RedemptionRateReportDto> GetRedemptionRateAsync(ReportPeriodDto input);

    Task<List<BranchComparisonDto>> GetBranchComparisonAsync(ReportPeriodDto input);

    Task<CustomerSegmentReportDto> GetCustomerSegmentsAsync();

    Task<List<TierDistributionDto>> GetTierDistributionAsync();

    Task<List<TopCustomerDto>> GetTopCustomersAsync(int count = 10);

    Task<CampaignPerformanceDto> GetCampaignPerformanceAsync(Guid campaignId);

    Task<List<NotificationDeliveryRateDto>> GetNotificationDeliveryRatesAsync(ReportPeriodDto input);

    Task<DownloadTokenResultDto> GetTransactionsDownloadTokenAsync();

    Task<IRemoteStreamContent> GetTransactionsAsExcelFileAsync(TransactionsExcelDownloadDto input);
}
