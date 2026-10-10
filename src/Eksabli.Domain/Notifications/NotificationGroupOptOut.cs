using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace Eksabli.Notifications;

// A customer switched a notification group off. It is per user, not per business: a customer's preference applies at every
// business they belong to. Absence of a row means the group is on, so the default for everyone is to receive everything.
public class NotificationGroupOptOut : CreationAuditedAggregateRoot<Guid>
{
    public Guid UserId { get; private set; }

    public NotificationGroup Group { get; private set; }

    protected NotificationGroupOptOut()
    {
        /* Required by the ORM */
    }

    private NotificationGroupOptOut(Guid id, Guid userId, NotificationGroup group)
        : base(id)
    {
        UserId = userId;
        Group = group;
    }

    public static NotificationGroupOptOut Create(Guid id, Guid userId, NotificationGroup group) => new(id, userId, group);
}
