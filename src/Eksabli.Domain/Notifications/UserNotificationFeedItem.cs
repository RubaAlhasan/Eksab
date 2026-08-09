using System;

namespace Eksabli.Notifications;

// Read model for "one row in my notification feed" — the UserNotification/NotificationMessage join,
// pre-flattened by the repository so the Application layer never needs to know the storage model is
// split in two.
public class UserNotificationFeedItem
{
    public Guid Id { get; set; } // UserNotification.Id — what MarkAsReadAsync takes
    public UserNotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Data { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreationTime { get; set; }
}
