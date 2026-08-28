using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.BusinessProfiles;
using Eksabli.Platform;
using Eksabli.Reporting;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace Eksabli.PlatformReports;

[RemoteService(IsEnabled = false)]
public class AdminPlatformReportAppService : ApplicationService, IAdminPlatformReportAppService
{
    private readonly IRepository<BusinessProfile, Guid> _businessProfileRepository;
    private readonly ISupportTicketRepository _supportTicketRepository;
    private readonly IDataFilter _dataFilter;

    public AdminPlatformReportAppService(
        IRepository<BusinessProfile, Guid> businessProfileRepository,
        ISupportTicketRepository supportTicketRepository,
        IDataFilter dataFilter)
    {
        _businessProfileRepository = businessProfileRepository;
        _supportTicketRepository = supportTicketRepository;
        _dataFilter = dataFilter;
    }

    // Trailing 7 months (this month inclusive), zero-filled — same window as
    // AdminSubscriptionAppService.GetMrrTrendAsync's MRR trend (both use TrailingMonths.Compute), so
    // the two charts on the Reports page read consistently. BusinessProfile is IMultiTenant, so this
    // needs the same Disable<IMultiTenant>() treatment as every other Host-realm cross-tenant query in
    // this codebase. Counting is done at the DB level (GroupBy translated to SQL via AsyncExecuter,
    // same pattern as GetTicketMetricsAsync below) rather than pulling every BusinessProfile row into
    // memory just to count them.
    public async Task<List<TenantGrowthPointDto>> GetTenantGrowthAsync()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var months = TrailingMonths.Compute(Clock.Now);
            var from = new DateTime(months[0].Year, months[0].Month, 1);

            var queryable = await _businessProfileRepository.GetQueryableAsync();

            var countByMonth = await AsyncExecuter.ToListAsync(
                queryable
                    .Where(p => p.CreationTime >= from)
                    .GroupBy(p => new { p.CreationTime.Year, p.CreationTime.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() }));

            var lookup = countByMonth.ToDictionary(x => (x.Year, x.Month), x => x.Count);

            return months
                .Select(m => new TenantGrowthPointDto
                {
                    Year = m.Year,
                    Month = m.Month,
                    NewTenants = lookup.GetValueOrDefault((m.Year, m.Month))
                })
                .ToList();
        }
    }

    // SupportTicket isn't IMultiTenant (it's manually TenantId-filtered, see
    // SupportTicketAppService/EfCoreSupportTicketRepository) — no Disable<IMultiTenant>() needed or
    // possible here; querying with no tenantId/customerId filter already returns every ticket.
    public async Task<SupportTicketMetricsDto> GetTicketMetricsAsync()
    {
        var queryable = await _supportTicketRepository.GetQueryableAsync();

        var byStatus = await AsyncExecuter.ToListAsync(
            queryable.GroupBy(t => t.Status).Select(g => new { Status = g.Key, Count = g.Count() }));

        var byPriority = await AsyncExecuter.ToListAsync(
            queryable.GroupBy(t => t.Priority).Select(g => new { Priority = g.Key, Count = g.Count() }));

        var countByStatus = byStatus.ToDictionary(x => x.Status, x => x.Count);
        var countByPriority = byPriority.ToDictionary(x => x.Priority, x => x.Count);

        return new SupportTicketMetricsDto
        {
            // "Still needs attention" — Open + InProgress, not just literal Status == Open.
            TotalOpen = countByStatus.GetValueOrDefault(SupportTicketStatus.Open)
                + countByStatus.GetValueOrDefault(SupportTicketStatus.InProgress),
            CountByStatus = countByStatus,
            CountByPriority = countByPriority,
        };
    }
}
