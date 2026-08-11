namespace Eksabli.Notifications;

// Who a UserNotification (and its real-time SignalR/FCM fan-out) was addressed to. Stored on the
// entity itself so history queries don't have to re-derive it from the nullability of UserId/TenantId.
public enum NotificationTargetType
{
    // One specific IdentityUser — UserId is set. TenantId mirrors that user's tenant (null for a
    // Host-realm user), it does not itself broaden the audience.
    User = 0,

    // Every user of one tenant (business) — TenantId is set, UserId is null.
    Tenant = 1,

    // Every user, across every tenant and the Host realm — UserId and TenantId are both null.
    Broadcast = 2
}
