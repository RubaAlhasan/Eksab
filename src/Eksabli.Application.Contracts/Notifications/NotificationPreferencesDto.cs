namespace Eksabli.Notifications;

// The customer's notification switches. true means the group is on. A group that is off is not delivered anywhere.
public class NotificationPreferencesDto
{
    public bool Rewards { get; set; } = true;

    public bool Deals { get; set; } = true;

    public bool Offers { get; set; } = true;
}
