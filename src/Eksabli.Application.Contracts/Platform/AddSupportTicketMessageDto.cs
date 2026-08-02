using System.ComponentModel.DataAnnotations;

namespace Eksabli.Platform;

public class AddSupportTicketMessageDto
{
    [Required]
    [StringLength(SupportTicketMessageConsts.MaxBodyLength)]
    public string Body { get; set; } = string.Empty;
}
