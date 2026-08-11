using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.AuditLogs;

// Mirrors a real subset of IAuditLogRepository.GetListAsync's own filter parameters (Volo.Abp
// .AuditLogging.Domain) — not inventing a filter shape, just exposing what that repository already
// supports. Left out: ClientId/CorrelationId/ExecutionDuration range (real too, but no MVP prototype
// or use case asked for them yet).
public class AdminAuditLogFilterDto : PagedAndSortedResultRequestDto
{
    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public string? HttpMethod { get; set; }

    public string? Url { get; set; }

    public string? UserName { get; set; }

    public bool? HasException { get; set; }

    public int? HttpStatusCode { get; set; }
}
