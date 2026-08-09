using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Notifications;

// The per-recipient half of the Notification Hub's storage model — see NotificationMessage for why
// content and recipient/read-state are split. One row is fanned out per actual recipient at publish
// time (by NotificationPublisher), so this row's TenantId is always the recipient's own tenant
// (host-realm user => null) and ABP's standard IMultiTenant data filter "just works" for feed queries —
// no manual cross-tenant querying needed on the read side, only on the fan-out/write side. Written only
// through INotificationPublisher.
public class UserNotification : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid NotificationMessageId { get; private set; }

    public Guid UserId { get; private set; }

    public bool IsRead { get; private set; }

    public DateTime? ReadAt { get; private set; }

    protected UserNotification()
    {
    }

    private UserNotification(Guid id, Guid notificationMessageId, Guid userId, Guid? tenantId)
        : base(id)
    {
        NotificationMessageId = notificationMessageId;
        UserId = userId;
        TenantId = tenantId;
        IsRead = false;
    }

    public static UserNotification Create(Guid id, Guid notificationMessageId, Guid userId, Guid? tenantId)
    {
        return new UserNotification(id, notificationMessageId, userId, tenantId);
    }

    public void MarkAsRead(DateTime readAt)
    {
        if (IsRead)
        {
            return;
        }

        IsRead = true;
        ReadAt = readAt;
    }
}
