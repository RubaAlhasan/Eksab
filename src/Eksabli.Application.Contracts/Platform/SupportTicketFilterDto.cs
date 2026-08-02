using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Platform;

public class SupportTicketFilterDto : PagedAndSortedResultRequestDto
{
    public SupportTicketStatus? Status { get; set; }

    public SupportTicketPriority? Priority { get; set; }

    public Guid? TenantId { get; set; }
}
