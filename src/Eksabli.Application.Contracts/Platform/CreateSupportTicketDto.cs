using System.ComponentModel.DataAnnotations;

namespace Eksabli.Platform;

public class CreateSupportTicketDto
{
    [Required]
    [StringLength(SupportTicketConsts.MaxSubjectLength)]
    public string Subject { get; set; } = string.Empty;

    // First message body — a ticket can't exist without an initial description of the problem.
    [Required]
    [StringLength(SupportTicketMessageConsts.MaxBodyLength)]
    public string Body { get; set; } = string.Empty;

    public SupportTicketPriority Priority { get; set; } = SupportTicketPriority.Medium;
}
