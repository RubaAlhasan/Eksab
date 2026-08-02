using System;

namespace Eksabli.Reports;

// "Opened" and "revenue attributed" aren't reportable yet — no read-receipt tracking on Notification,
// and no monetary amount stored on the PointsTransaction rows a campaign bonus creates.
public class CampaignPerformanceDto
{
    public Guid CampaignId { get; set; }

    public int NotificationsSent { get; set; }

    public int NotificationsQueued { get; set; }

    public int NotificationsFailed { get; set; }

    public int BonusPointsAwarded { get; set; }

    public int MembershipsRewarded { get; set; }
}
