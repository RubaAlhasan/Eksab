using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Memberships;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Notifications;

[RemoteService(IsEnabled = false)]
public class NotificationAppService : ApplicationService, INotificationAppService
{
    private readonly INotificationRepository _repository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly INotificationPublisher _notificationPublisher;

    public NotificationAppService(
        INotificationRepository repository,
        IRepository<Membership, Guid> membershipRepository,
        IBackgroundJobManager backgroundJobManager,
        INotificationPublisher notificationPublisher)
    {
        _repository = repository;
        _membershipRepository = membershipRepository;
        _backgroundJobManager = backgroundJobManager;
        _notificationPublisher = notificationPublisher;
    }

    public async Task<NotificationDto> SendAsync(SendNotificationDto input)
    {
        var membership = await _membershipRepository.GetAsync(input.MembershipId); // 404s if not this tenant's member

        var notification = Notification.Create(GuidGenerator.Create(), input.MembershipId, input.Channel, input.Title, input.Body);
        await _repository.InsertAsync(notification);

        await _backgroundJobManager.EnqueueAsync(new NotificationDispatchArgs { NotificationId = notification.Id });

        // Same gap this session already fixed for CampaignSweepWorker/ReferralCompletionService: the
        // Notification row above is a delivery *attempt* record (Push/Email/Sms/InApp — none of which
        // have a real provider configured, see NotificationSender's own comment), not what the
        // customer's in-app inbox (/customer/alerts, UserNotification) actually reads. Without this, a
        // staff "Compose" send never reached the customer regardless of chosen channel.
        await _notificationPublisher.PublishToUserAsync(
            membership.CustomerId,
            CurrentTenant.Id,
            UserNotificationType.Info,
            input.Title,
            input.Body,
            category: "Manual");

        return ObjectMapper.Map<Notification, NotificationDto>(notification);
    }

    public async Task<PagedResultDto<NotificationDto>> GetListAsync(NotificationListFilterDto input)
    {
        var (notifications, totalCount) = await _repository.GetListAsync(
            campaignId: input.CampaignId,
            status: input.Status,
            channel: input.Channel,
            sorting: input.Sorting,
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount);

        return new PagedResultDto<NotificationDto>(totalCount, ObjectMapper.Map<List<Notification>, List<NotificationDto>>(notifications));
    }

    public async Task<NotificationQuotaUsageDto> GetQuotaUsageAsync()
    {
        var sentToday = await _repository.CountCreatedSinceAsync(Clock.Now.Date);
        return new NotificationQuotaUsageDto
        {
            SentToday = sentToday,
            DailyLimit = NotificationConsts.MaxDailyNotificationsPerTenant
        };
    }
}
