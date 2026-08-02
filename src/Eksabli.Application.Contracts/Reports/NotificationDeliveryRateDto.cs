using Eksabli.Notifications;

namespace Eksabli.Reports;

public class NotificationDeliveryRateDto
{
    public NotificationChannel Channel { get; set; }

    public int Sent { get; set; }

    public int Failed { get; set; }

    public int Queued { get; set; }

    // Sent / (Sent + Failed) — Queued isn't counted as an attempt yet since it hasn't been dispatched.
    // 0 when there have been no dispatch attempts, to avoid a divide-by-zero.
    public decimal DeliveryRate { get; set; }
}
