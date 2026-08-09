using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.AuditLogging;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;

namespace Eksabli.AuditLogs;

[RemoteService(IsEnabled = false)]
public class AdminAuditLogAppService : ApplicationService, IAdminAuditLogAppService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IDataFilter _dataFilter;

    public AdminAuditLogAppService(IAuditLogRepository auditLogRepository, IDataFilter dataFilter)
    {
        _auditLogRepository = auditLogRepository;
        _dataFilter = dataFilter;
    }

    public async Task<PagedResultDto<AuditLogDto>> GetListAsync(AdminAuditLogFilterDto input)
    {
        // AuditLog implements IMultiTenant (confirmed by reflecting Volo.Abp.AuditLogging.AuditLog) —
        // a platform admin needs to see every tenant's requests, not just the ambient (Host, i.e. null
        // TenantId) ones, same "cross-tenant Host view" shape AdminSubscriptionAppService already uses.
        // IAuditLogRepository's own httpStatusCode filter is typed System.Net.HttpStatusCode?, not
        // int? — AdminAuditLogFilterDto keeps the DTO/wire shape a plain int (matching AuditLogDto's
        // own HttpStatusCode field and every other int-typed status code already used elsewhere in
        // this app, e.g. InvoiceDto), converted only at this one real call boundary.
        HttpStatusCode? httpStatusCode = input.HttpStatusCode.HasValue ? (HttpStatusCode)input.HttpStatusCode.Value : null;

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var logs = await _auditLogRepository.GetListAsync(
                sorting: input.Sorting.IsNullOrWhiteSpace() ? "executionTime desc" : input.Sorting,
                maxResultCount: input.MaxResultCount,
                skipCount: input.SkipCount,
                startTime: input.StartTime,
                endTime: input.EndTime,
                httpMethod: input.HttpMethod,
                url: input.Url,
                userName: input.UserName,
                hasException: input.HasException,
                httpStatusCode: httpStatusCode);

            var totalCount = await _auditLogRepository.GetCountAsync(
                startTime: input.StartTime,
                endTime: input.EndTime,
                httpMethod: input.HttpMethod,
                url: input.Url,
                userName: input.UserName,
                hasException: input.HasException,
                httpStatusCode: httpStatusCode);

            return new PagedResultDto<AuditLogDto>((int)totalCount, MapToDtos(logs));
        }
    }

    private static List<AuditLogDto> MapToDtos(List<AuditLog> logs)
    {
        return logs.Select(log => new AuditLogDto
        {
            Id = log.Id,
            UserId = log.UserId,
            UserName = log.UserName,
            TenantId = log.TenantId,
            TenantName = log.TenantName,
            ApplicationName = log.ApplicationName,
            ExecutionTime = log.ExecutionTime,
            ExecutionDuration = log.ExecutionDuration,
            ClientIpAddress = log.ClientIpAddress,
            HttpMethod = log.HttpMethod,
            Url = log.Url,
            HttpStatusCode = log.HttpStatusCode,
            HasException = !log.Exceptions.IsNullOrWhiteSpace()
        }).ToList();
    }
}
