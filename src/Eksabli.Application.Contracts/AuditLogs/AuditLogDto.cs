using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.AuditLogs;

// Deliberately thinner than ABP's own AuditLog entity — no EntityChanges/Actions/BrowserInfo/
// ExtraProperties (a detail drill-down is real future scope, not built this pass; see
// IAdminAuditLogAppService's own doc comment). Just enough to render a real request-log table.
public class AuditLogDto : EntityDto<Guid>
{
    public Guid? UserId { get; set; }

    public string? UserName { get; set; }

    public Guid? TenantId { get; set; }

    public string? TenantName { get; set; }

    public string? ApplicationName { get; set; }

    public DateTime ExecutionTime { get; set; }

    public int ExecutionDuration { get; set; }

    public string? ClientIpAddress { get; set; }

    public string? HttpMethod { get; set; }

    public string? Url { get; set; }

    public int? HttpStatusCode { get; set; }

    public bool HasException { get; set; }
}
