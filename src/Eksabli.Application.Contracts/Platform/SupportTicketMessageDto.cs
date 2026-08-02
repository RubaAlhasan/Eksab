using System;

namespace Eksabli.Platform;

public class SupportTicketMessageDto
{
    public Guid Id { get; set; }

    public Guid TicketId { get; set; }

    public Guid SenderId { get; set; }

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
