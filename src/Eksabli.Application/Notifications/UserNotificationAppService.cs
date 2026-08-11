using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Users;
using Volo.Abp.Validation;

namespace Eksabli.Notifications;

[RemoteService(IsEnabled = false)]
public class UserNotificationAppService : ApplicationService, IUserNotificationAppService
{
    private readonly IUserNotificationRepository _repository;
    private readonly INotificationPublisher _notificationPublisher;

    public UserNotificationAppService(IUserNotificationRepository repository, INotificationPublisher notificationPublisher)
    {
        _repository = repository;
        _notificationPublisher = notificationPublisher;
    }

    public async Task<PagedResultDto<UserNotificationDto>> GetListAsync(UserNotificationListFilterDto input)
    {
        var (items, totalCount) = await _repository.GetFeedAsync(
            CurrentUser.GetId(), input.IsRead, input.SkipCount, input.MaxResultCount);

        return new PagedResultDto<UserNotificationDto>(totalCount, ObjectMapper.Map<List<UserNotificationFeedItem>, List<UserNotificationDto>>(items));
    }

    public async Task<int> GetUnreadCountAsync()
    {
        var count = await _repository.GetUnreadCountAsync(CurrentUser.GetId());
        return (int)count;
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        var userId = CurrentUser.GetId();
        var notification = await _repository.GetAsync(id);
        if (notification.UserId != userId)
        {
            throw new AbpAuthorizationException("You can only mark your own notifications as read.");
        }

        notification.MarkAsRead(Clock.Now);
        await _repository.UpdateAsync(notification);
    }

    public async Task MarkAllAsReadAsync()
    {
        await _repository.MarkAllAsReadAsync(CurrentUser.GetId(), Clock.Now);
    }

    // Permission-gated at the controller (EksabliPermissions.Notifications.Broadcast) — same treatment
    // as every other permission check in this codebase, see NotificationsController.
    public async Task SendAsync(SendUserNotificationDto input)
    {
        switch (input.TargetType)
        {
            case NotificationTargetType.User:
                if (input.UserId == null)
                {
                    throw new AbpValidationException("UserId is required when TargetType is User.");
                }

                await _notificationPublisher.PublishToUserAsync(
                    input.UserId.Value, input.TenantId, input.Type, input.Title, input.Message, input.Category);
                break;

            case NotificationTargetType.Tenant:
                if (input.TenantId == null)
                {
                    throw new AbpValidationException("TenantId is required when TargetType is Tenant.");
                }

                await _notificationPublisher.PublishToTenantAsync(
                    input.TenantId.Value, input.Type, input.Title, input.Message, input.Category);
                break;

            case NotificationTargetType.Broadcast:
                await _notificationPublisher.PublishToAllAsync(input.Type, input.Title, input.Message, input.Category);
                break;

            default:
                throw new AbpException($"Unknown {nameof(NotificationTargetType)}: {input.TargetType}");
        }
    }
}
