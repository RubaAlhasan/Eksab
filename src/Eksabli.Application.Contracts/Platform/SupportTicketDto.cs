using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Platform;

public class SupportTicketDto : FullAuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }

    public Guid? CustomerId { get; set; }

    public string Subject { get; set; } = string.Empty;

    public SupportTicketStatus Status { get; set; }

    public SupportTicketPriority Priority { get; set; }

    // Only populated by GetAsync (the detail/thread view) — GetListAsync (the queue) omits it.
    public List<SupportTicketMessageDto> Messages { get; set; } = new();
}
