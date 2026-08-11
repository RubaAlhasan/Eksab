using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Eksabli.Notifications;

// The content half of the Notification Hub's storage model — one row per application event, however
// many recipients it fans out to. Deliberately NOT IMultiTenant: a Tenant/Broadcast message is
// addressed to many tenants' users at once, so it can't itself belong to a single tenant's data filter.
// "Who got it and did they read it" lives on UserNotification (one row per recipient) instead — the
// same message/recipient split ABP Commercial's own Notification System uses, for the same reason:
// per-recipient read state on a shared broadcast row would mean one person's "read" marks it read for
// everyone.
public class NotificationMessage : AuditedAggregateRoot<Guid>
{
    public NotificationTargetType TargetType { get; private set; }

    // Informational only (audit/debugging) — the tenant this was addressed to when TargetType ==
    // Tenant, or the recipient's tenant when TargetType == User. Not used for query filtering; see
    // UserNotification.TenantId for that.
    public Guid? SourceTenantId { get; private set; }

    public UserNotificationType Type { get; private set; }

    public string Title { get; private set; }

    public string Message { get; private set; }

    public string? Category { get; private set; }

    public string? Data { get; private set; }

    protected NotificationMessage()
    {
        Title = string.Empty;
        Message = string.Empty;
    }

    private NotificationMessage(
        Guid id, NotificationTargetType targetType, Guid? sourceTenantId, UserNotificationType type,
        string title, string message, string? category, string? data)
        : base(id)
    {
        TargetType = targetType;
        SourceTenantId = sourceTenantId;
        Type = type;
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), UserNotificationConsts.MaxTitleLength);
        Message = Check.NotNullOrWhiteSpace(message, nameof(message), UserNotificationConsts.MaxMessageLength);
        Category = Truncate(category, UserNotificationConsts.MaxCategoryLength);
        Data = Truncate(data, UserNotificationConsts.MaxDataLength);
    }

    public static NotificationMessage Create(
        Guid id, NotificationTargetType targetType, Guid? sourceTenantId, UserNotificationType type,
        string title, string message, string? category = null, string? data = null)
    {
        return new NotificationMessage(id, targetType, sourceTenantId, type, title, message, category, data);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        return value == null || value.Length <= maxLength ? value : value[..maxLength];
    }
}
