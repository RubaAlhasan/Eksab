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

    // Detail drill-down for a single row — Actions (request payload) + EntityChanges (closest real
    // "what happened" data; see AuditLogDetailDto's own comment for why there's no literal response
    // body). Fetched on demand rather than included on every list row: GetListAsync's own AuditLogDto
    // stays the same lightweight shape it always was, this is a separate, heavier call.
    public async Task<AuditLogDetailDto> GetAsync(Guid id)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var log = await _auditLogRepository.GetAsync(id, includeDetails: true);
            return MapToDetailDto(log);
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

    private static AuditLogDetailDto MapToDetailDto(AuditLog log)
    {
        return new AuditLogDetailDto
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
            HasException = !log.Exceptions.IsNullOrWhiteSpace(),
            Comments = log.Comments,
            Exceptions = log.Exceptions,
            Actions = log.Actions?.Select(a => new AuditLogActionDto
            {
                ServiceName = a.ServiceName,
                MethodName = a.MethodName,
                Parameters = a.Parameters,
                ExecutionTime = a.ExecutionTime,
                ExecutionDuration = a.ExecutionDuration
            }).ToList() ?? [],
            EntityChanges = log.EntityChanges?.Select(c => new AuditLogEntityChangeDto
            {
                EntityTypeFullName = c.EntityTypeFullName,
                EntityId = c.EntityId,
                ChangeType = (int)c.ChangeType,
                ChangeTime = c.ChangeTime,
                PropertyChanges = c.PropertyChanges?.Select(p => new AuditLogEntityPropertyChangeDto
                {
                    PropertyName = p.PropertyName,
                    OriginalValue = p.OriginalValue,
                    NewValue = p.NewValue
                }).ToList() ?? []
            }).ToList() ?? []
        };
    }
}
