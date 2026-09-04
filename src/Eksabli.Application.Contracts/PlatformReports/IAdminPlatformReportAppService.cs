using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace Eksabli.PlatformReports;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/AdminPlatformReportsController.cs),
// matching IAdminSubscriptionAppService's convention. Deliberately does NOT re-expose category-mix or
// MRR — CategoryDto.BusinessCount (api/app/category) and AdminSubscriptionAppService.GetStatsAsync/
// GetMrrTrendAsync (api/app/admin-subscriptions/stats|mrr-trend) already cover those; the Angular
// Reports page calls those existing endpoints directly instead of duplicating them here.
[RemoteService(IsEnabled = false)]
public interface IAdminPlatformReportAppService : IApplicationService
{
    Task<List<TenantGrowthPointDto>> GetTenantGrowthAsync();

    Task<SupportTicketMetricsDto> GetTicketMetricsAsync();
}
