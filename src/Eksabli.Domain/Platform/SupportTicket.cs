using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Eksabli.Platform;

// Platform support — deliberately NOT IMultiTenant: a ticket can come from a business (TenantId set),
// a customer (CustomerId set, Host realm), or neither, and Support Agents need to see the whole queue
// regardless of tenant. Same "manually filtered, not auto-scoped" treatment as Engagement.Achievement.
public class SupportTicket : FullAuditedAggregateRoot<Guid>
{
    public Guid? TenantId { get; private set; }

    public Guid? CustomerId { get; private set; }

    public string Subject { get; private set; }

    public SupportTicketStatus Status { get; private set; }

    public SupportTicketPriority Priority { get; private set; }

    private readonly List<SupportTicketMessage> _messages = new();

    // Child collection — no separate repository, only reachable through this aggregate.
    public IReadOnlyCollection<SupportTicketMessage> Messages => _messages;

    protected SupportTicket()
    {
        Subject = string.Empty;
    }

    private SupportTicket(
        Guid id, Guid? tenantId, Guid? customerId, string subject, SupportTicketPriority priority,
        Guid initialMessageId, Guid reporterId, string body, DateTime createdAt)
        : base(id)
    {
        TenantId = tenantId;
        CustomerId = customerId;
        Subject = Check.NotNullOrWhiteSpace(subject, nameof(subject), SupportTicketConsts.MaxSubjectLength);
        Priority = priority;
        Status = SupportTicketStatus.Open;

        // The opening message doesn't go through AddMessage — it's what puts the ticket in the queue
        // in the first place, not a reply, so it must not trip AddMessage's Open -> InProgress step.
        _messages.Add(SupportTicketMessage.Create(initialMessageId, Id, reporterId, body, createdAt));
    }

    public static SupportTicket Create(
        Guid id, Guid? tenantId, Guid? customerId, string subject, SupportTicketPriority priority,
        Guid initialMessageId, Guid reporterId, string body, DateTime createdAt)
    {
        return new SupportTicket(id, tenantId, customerId, subject, priority, initialMessageId, reporterId, body, createdAt);
    }

    public SupportTicketMessage AddMessage(Guid id, Guid senderId, string body, DateTime createdAt)
    {
        if (Status == SupportTicketStatus.Closed)
        {
            throw new UserFriendlyException("This ticket is closed and can no longer receive replies.");
        }

        var message = SupportTicketMessage.Create(id, Id, senderId, body, createdAt);
        _messages.Add(message);

        // A reply from support reopens a resolved ticket if the reporter wasn't satisfied — but
        // AddMessage doesn't know who's replying, so it only auto-advances Open -> InProgress; explicit
        // Resolve()/Close() calls own the rest of the state machine.
        if (Status == SupportTicketStatus.Open)
        {
            Status = SupportTicketStatus.InProgress;
        }

        return message;
    }

    public void SetPriority(SupportTicketPriority priority) => Priority = priority;

    public void Resolve()
    {
        if (Status == SupportTicketStatus.Closed)
        {
            throw new UserFriendlyException("This ticket is already closed.");
        }

        Status = SupportTicketStatus.Resolved;
    }

    public void Close()
    {
        Status = SupportTicketStatus.Closed;
    }

    public void Reopen()
    {
        if (Status != SupportTicketStatus.Resolved)
        {
            throw new UserFriendlyException("Only a resolved ticket can be reopened.");
        }

        Status = SupportTicketStatus.InProgress;
    }
}
