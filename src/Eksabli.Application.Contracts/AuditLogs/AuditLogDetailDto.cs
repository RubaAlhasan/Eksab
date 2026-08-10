using System;
using System.Collections.Generic;

namespace Eksabli.AuditLogs;

// Detail drill-down for a single audit log row — everything AuditLogDto's MVP list shape leaves out.
// "Request payload" is real: AuditLogAction.Parameters is the JSON-serialized argument set ABP's own
// auditing interceptor captures for every application-service method call within the request.
// There is no literal "response body" anywhere in ABP's OSS auditing data (confirmed — Volo.Abp
// .AuditLogging.AuditLog has no ReturnValue/response field at all, by design: it audits the request
// and its side effects, not a response payload capture). EntityChanges — the actual before/after
// property values of every entity the request created/updated/deleted — is the closest real
// "what happened as a result" data that exists, so that's what's shown in that slot instead of
// fabricating a response body that was never captured.
public class AuditLogDetailDto : AuditLogDto
{
    public string? Comments { get; set; }

    public string? Exceptions { get; set; }

    public List<AuditLogActionDto> Actions { get; set; } = [];

    public List<AuditLogEntityChangeDto> EntityChanges { get; set; } = [];
}

public class AuditLogActionDto
{
    public string? ServiceName { get; set; }

    public string? MethodName { get; set; }

    // Raw JSON, same shape ABP itself stores it in — rendered pretty-printed client-side rather than
    // re-parsed/re-shaped server-side, since its structure is whatever the called method's parameters
    // happen to be (no fixed schema to map onto).
    public string? Parameters { get; set; }

    public DateTime ExecutionTime { get; set; }

    public int ExecutionDuration { get; set; }
}

public class AuditLogEntityChangeDto
{
    public string? EntityTypeFullName { get; set; }

    public string? EntityId { get; set; }

    // Volo.Abp.Auditing.EntityChangeType as its underlying int (Created = 0, Updated = 1, Deleted = 2)
    // — kept a plain int here rather than referencing that enum type directly, same "don't leak a
    // framework-internal enum through this app's own DTO surface" treatment as HttpStatusCode above.
    public int ChangeType { get; set; }

    public DateTime ChangeTime { get; set; }

    public List<AuditLogEntityPropertyChangeDto> PropertyChanges { get; set; } = [];
}

public class AuditLogEntityPropertyChangeDto
{
    public string? PropertyName { get; set; }

    public string? OriginalValue { get; set; }

    public string? NewValue { get; set; }
}
