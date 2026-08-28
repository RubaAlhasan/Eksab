using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.BusinessProfiles;
using Eksabli.Platform;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Timing;
using Xunit;

namespace Eksabli.PlatformReports;

public abstract class AdminPlatformReportAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAdminPlatformReportAppService _adminPlatformReportAppService;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<BusinessProfile, Guid> _businessProfileRepository;
    private readonly ISupportTicketRepository _supportTicketRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;

    protected AdminPlatformReportAppService_Tests()
    {
        _adminPlatformReportAppService = GetRequiredService<IAdminPlatformReportAppService>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _businessProfileRepository = GetRequiredService<IRepository<BusinessProfile, Guid>>();
        _supportTicketRepository = GetRequiredService<ISupportTicketRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _clock = GetRequiredService<IClock>();
    }

    [Fact]
    public async Task GetTenantGrowthAsync_Should_Count_A_New_Tenant_In_The_Current_Month()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var tenant = await _tenantManager.CreateAsync("tenant-" + Guid.NewGuid().ToString("N"));
            await _tenantRepository.InsertAsync(tenant, autoSave: true);

            using (_currentTenant.Change(tenant.Id))
            {
                await _businessProfileRepository.InsertAsync(BusinessProfile.Create(Guid.NewGuid()), autoSave: true);
            }
        });

        var points = await WithUnitOfWorkAsync(() => _adminPlatformReportAppService.GetTenantGrowthAsync());

        points.Count.ShouldBe(7);
        var now = _clock.Now;
        var currentMonth = points.Single(p => p.Year == now.Year && p.Month == now.Month);
        currentMonth.NewTenants.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetTicketMetricsAsync_Should_Count_Tickets_Across_Every_Tenant()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var reporterId = Guid.NewGuid();
            var ticket = SupportTicket.Create(
                Guid.NewGuid(), null, reporterId, "Can't redeem a coupon", SupportTicketPriority.High,
                Guid.NewGuid(), reporterId, "The QR code isn't scanning at checkout.", _clock.Now);
            await _supportTicketRepository.InsertAsync(ticket, autoSave: true);
        });

        var metrics = await WithUnitOfWorkAsync(() => _adminPlatformReportAppService.GetTicketMetricsAsync());

        metrics.TotalOpen.ShouldBeGreaterThanOrEqualTo(1);
        metrics.CountByStatus.GetValueOrDefault(SupportTicketStatus.Open).ShouldBeGreaterThanOrEqualTo(1);
        metrics.CountByPriority.GetValueOrDefault(SupportTicketPriority.High).ShouldBeGreaterThanOrEqualTo(1);
    }
}
