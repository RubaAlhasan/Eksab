using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace Eksabli.Platform;

// Child entity — no repository of its own; only reachable through its owning SupportTicket, same
// treatment as Campaigns.CampaignTargetRule.
public class SupportTicketMessage : Entity<Guid>
{
    public Guid TicketId { get; private set; }

    // Soft reference — the Host-realm IdentityUser.Id of whoever wrote this message (reporter or
    // Support Agent alike; no separate "is this staff" flag needed since that's derivable from role).
    public Guid SenderId { get; private set; }

    public string Body { get; private set; }

    public DateTime CreatedAt { get; private set; }

    protected SupportTicketMessage()
    {
        Body = string.Empty;
    }

    private SupportTicketMessage(Guid id, Guid ticketId, Guid senderId, string body, DateTime createdAt)
        : base(id)
    {
        TicketId = ticketId;
        SenderId = senderId;
        Body = Check.NotNullOrWhiteSpace(body, nameof(body), SupportTicketMessageConsts.MaxBodyLength);
        CreatedAt = createdAt;
    }

    // internal — SupportTicket.AddMessage is the only sanctioned entry point (same assembly).
    internal static SupportTicketMessage Create(Guid id, Guid ticketId, Guid senderId, string body, DateTime createdAt)
    {
        return new SupportTicketMessage(id, ticketId, senderId, body, createdAt);
    }
}
