using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace Eksabli.Dashboards;

// Business Portal Dashboard 360. Tenant-scoped throughout: the business is taken from the ambient tenant, never from the
// request, so a business can't ask for another business's numbers. Exposed via an explicit controller
// (src/Eksabli.HttpApi/Controllers/BusinessDashboardController.cs), gated on Eksabli.Reports.Default like ReportsController.
[RemoteService(IsEnabled = false)]
public interface IBusinessDashboardAppService : IApplicationService
{
    Task<BusinessDashboardSummaryDto> GetSummaryAsync(DashboardRangeDto input);

    Task<List<BusinessDashboardTrendPointDto>> GetTrendsAsync(DashboardRangeDto input);

    Task<List<PeakHourCellDto>> GetPeakHoursAsync(DashboardRangeDto input);

    Task<List<BusinessOfferPerformanceDto>> GetOfferPerformanceAsync(DashboardRangeDto input);

    Task<List<BusinessInsightDto>> GetInsightsAsync();
}
