using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Notifications;

// Delivery record for one message to one customer (or a broadcast when MembershipId is null). Also
// what the Reports feature reads for delivery stats — see
// docs/eksabli-loyalty-platform/03-database-design.md#campaigns--notifications.
public class Notification : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? MembershipId { get; private set; }

    public Guid? TenantId { get; private set; }

    public Guid? CampaignId { get; private set; }

    public NotificationChannel Channel { get; private set; }

    public string Title { get; private set; }

    public string Body { get; private set; }

    public NotificationStatus Status { get; private set; }

    public DateTime? SentAt { get; private set; }

    protected Notification()
    {
        Title = string.Empty;
        Body = string.Empty;
    }

    private Notification(Guid id, Guid? membershipId, NotificationChannel channel, string title, string body, Guid? campaignId)
        : base(id)
    {
        MembershipId = membershipId;
        Channel = channel;
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), NotificationConsts.MaxTitleLength);
        Body = Check.NotNullOrWhiteSpace(body, nameof(body), NotificationConsts.MaxBodyLength);
        CampaignId = campaignId;
        Status = NotificationStatus.Queued;
    }

    public static Notification Create(Guid id, Guid? membershipId, NotificationChannel channel, string title, string body, Guid? campaignId = null)
    {
        return new Notification(id, membershipId, channel, title, body, campaignId);
    }

    public void MarkSent(DateTime sentAt)
    {
        Status = NotificationStatus.Sent;
        SentAt = sentAt;
    }

    public void MarkFailed()
    {
        Status = NotificationStatus.Failed;
    }
}
