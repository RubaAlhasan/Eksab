using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace Eksabli.Dashboards;

// Admin Portal Dashboard 360. Host-only: the whole controller is gated on Eksabli.PlatformReports.Default, and the
// money and ticket sections are additionally gated per section (see AdminDashboardSummaryDto). Exposed via an explicit
// controller (src/Eksabli.HttpApi/Controllers/AdminDashboardController.cs).
[RemoteService(IsEnabled = false)]
public interface IAdminDashboardAppService : IApplicationService
{
    Task<AdminDashboardSummaryDto> GetSummaryAsync(DashboardRangeDto input);

    Task<List<AdminDashboardTrendPointDto>> GetTrendsAsync(DashboardRangeDto input);

    Task<List<TopBusinessDto>> GetTopBusinessesAsync(DashboardRangeDto input);

    Task<List<TopOfferDto>> GetTopOffersAsync(DashboardRangeDto input);

    Task<AdminAlertsDto> GetAlertsAsync();

    Task<List<AdminActivityItemDto>> GetActivityAsync();
}
