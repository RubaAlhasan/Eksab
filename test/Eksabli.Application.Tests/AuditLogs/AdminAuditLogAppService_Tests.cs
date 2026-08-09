using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Auditing;
using Volo.Abp.Modularity;
using Xunit;

namespace Eksabli.AuditLogs;

public abstract class AdminAuditLogAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAdminAuditLogAppService _adminAuditLogAppService;
    private readonly IAuditingStore _auditingStore;

    protected AdminAuditLogAppService_Tests()
    {
        _adminAuditLogAppService = GetRequiredService<IAdminAuditLogAppService>();
        _auditingStore = GetRequiredService<IAuditingStore>();
    }

    // Goes through ABP's own real recording path (IAuditingStore.SaveAsync — the exact same call
    // ABP's built-in auditing interceptor/middleware makes for every real request) rather than
    // hand-constructing an AuditLog entity directly, so this test exercises the real write path too,
    // not just the read side this app service adds.
    private Task SaveAuditLogAsync(string userName, bool hasException = false)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            var info = new AuditLogInfo
            {
                ApplicationName = "Eksabli",
                ExecutionTime = DateTime.UtcNow,
                ExecutionDuration = 10,
                HttpMethod = "GET",
                Url = "/api/app/test",
                UserName = userName,
            };

            if (hasException)
            {
                info.Exceptions.Add(new Exception("boom"));
            }

            await _auditingStore.SaveAsync(info);
        });
    }

    [Fact]
    public async Task Should_List_Recorded_Audit_Logs()
    {
        var userName = "alice-" + Guid.NewGuid();
        await SaveAuditLogAsync(userName);

        var result = await WithUnitOfWorkAsync(() => _adminAuditLogAppService.GetListAsync(new AdminAuditLogFilterDto { MaxResultCount = 100 }));

        result.Items.Select(x => x.UserName).ShouldContain(userName);
    }

    [Fact]
    public async Task Should_Filter_By_UserName()
    {
        await SaveAuditLogAsync("carol-" + Guid.NewGuid());
        var uniqueName = "dave-" + Guid.NewGuid();
        await SaveAuditLogAsync(uniqueName);

        var result = await WithUnitOfWorkAsync(() =>
            _adminAuditLogAppService.GetListAsync(new AdminAuditLogFilterDto { UserName = uniqueName, MaxResultCount = 100 }));

        result.Items.ShouldNotBeEmpty();
        result.Items.ShouldAllBe(x => x.UserName == uniqueName);
    }

    [Fact]
    public async Task Should_Filter_By_HasException()
    {
        var name = "eve-" + Guid.NewGuid();
        await SaveAuditLogAsync(name, hasException: true);

        var result = await WithUnitOfWorkAsync(() =>
            _adminAuditLogAppService.GetListAsync(new AdminAuditLogFilterDto { UserName = name, HasException = true, MaxResultCount = 100 }));

        result.Items.ShouldContain(x => x.UserName == name && x.HasException);
    }
}
