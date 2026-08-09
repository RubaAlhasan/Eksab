namespace Eksabli.Notifications;

// Severity/style classification for a UserNotification — drives the icon/color the Angular bell
// dropdown and Flutter notification list render with. Deliberately small and generic: the specific
// business event (e.g. "SubscriptionRenewed", "AuditAlert") is carried in UserNotification.Category,
// not encoded here.
public enum UserNotificationType
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Error = 3
}
