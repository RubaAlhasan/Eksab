using System;

namespace Eksabli.Notifications;

public static class NotificationGroups
{
    // Maps a notification's category to the group a customer can turn off. Null means the category cannot be turned off:
    // billing notices go to business staff, who have no notification preferences screen, and a support reply is an
    // answer to something the customer themselves asked for, not the kind of thing "Offers and announcements" covers.
    public static NotificationGroup? Of(string? category)
    {
        if (string.IsNullOrEmpty(category))
        {
            return NotificationGroup.Offers;
        }

        if (category.StartsWith("billing.", StringComparison.Ordinal) || category.StartsWith("support.", StringComparison.Ordinal))
        {
            return null;
        }

        if (category.StartsWith("points.", StringComparison.Ordinal) || category.StartsWith("reward.", StringComparison.Ordinal))
        {
            return NotificationGroup.Rewards;
        }

        if (category.StartsWith("smartdeal.", StringComparison.Ordinal))
        {
            return NotificationGroup.Deals;
        }

        // Manual messages from the business, referral news and campaign notices.
        return NotificationGroup.Offers;
    }
}
