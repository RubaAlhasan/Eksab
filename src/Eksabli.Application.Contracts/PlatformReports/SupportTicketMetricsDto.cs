using System.Collections.Generic;
using Eksabli.Platform;

namespace Eksabli.PlatformReports;

// Platform-wide support-ticket volume only — deliberately no resolution-time metric: SupportTicket has
// no ResolvedAt/ClosedAt field, and LastModificationTime isn't a safe proxy (AddMessage bumps it on
// every reply, not just on resolution). Add a real ResolvedAt column first if that metric is wanted.
public class SupportTicketMetricsDto
{
    public int TotalOpen { get; set; }

    public Dictionary<SupportTicketStatus, int> CountByStatus { get; set; } = new();

    public Dictionary<SupportTicketPriority, int> CountByPriority { get; set; } = new();
}
