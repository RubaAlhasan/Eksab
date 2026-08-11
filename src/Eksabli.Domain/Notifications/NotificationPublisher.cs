using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eksabli.Devices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace Eksabli.Notifications;

// See INotificationPublisher for the "why". Persistence always happens first and is never skipped —
// even if the real-time push and/or FCM push below fail, the recipient still sees the notification next
// time they open the feed, so those two steps are best-effort and never allowed to fail this call.
public class NotificationPublisher : INotificationPublisher, ITransientDependency
{
    private readonly IGuidGenerator _guidGenerator;
    private readonly IRepository<NotificationMessage, Guid> _messageRepository;
    private readonly IUserNotificationRepository _userNotificationRepository;
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<Device, Guid> _deviceRepository;
    private readonly IPushNotificationSender _pushSender;
    private readonly IRealTimeNotifier _realTimeNotifier;
    private readonly ICurrentTenant _currentTenant;

    public ILogger<NotificationPublisher> Logger { get; set; } = NullLogger<NotificationPublisher>.Instance;

    public NotificationPublisher(
        IGuidGenerator guidGenerator,
        IRepository<NotificationMessage, Guid> messageRepository,
        IUserNotificationRepository userNotificationRepository,
        IIdentityUserRepository identityUserRepository,
        ITenantRepository tenantRepository,
        IRepository<Device, Guid> deviceRepository,
        IPushNotificationSender pushSender,
        IRealTimeNotifier realTimeNotifier,
        ICurrentTenant currentTenant)
    {
        _guidGenerator = guidGenerator;
        _messageRepository = messageRepository;
        _userNotificationRepository = userNotificationRepository;
        _identityUserRepository = identityUserRepository;
        _tenantRepository = tenantRepository;
        _deviceRepository = deviceRepository;
        _pushSender = pushSender;
        _realTimeNotifier = realTimeNotifier;
        _currentTenant = currentTenant;
    }

    public async Task PublishToUserAsync(
        Guid userId, Guid? tenantId, UserNotificationType type, string title, string message,
        string? category = null, object? data = null, CancellationToken cancellationToken = default)
    {
        var notificationMessage = await CreateMessageAsync(
            NotificationTargetType.User, tenantId, type, title, message, category, data, cancellationToken);

        UserNotification recipientRow;
        using (_currentTenant.Change(tenantId))
        {
            recipientRow = UserNotification.Create(_guidGenerator.Create(), notificationMessage.Id, userId, tenantId);
            await _userNotificationRepository.InsertAsync(recipientRow, cancellationToken: cancellationToken);
        }

        var payload = ToPayload(recipientRow.Id, notificationMessage);
        await TryAsync(() => _realTimeNotifier.NotifyUserAsync(userId, payload, cancellationToken), "real-time push to user");
        await TryPushToDevicesAsync(userId, notificationMessage);
    }

    public async Task PublishToTenantAsync(
        Guid tenantId, UserNotificationType type, string title, string message,
        string? category = null, object? data = null, CancellationToken cancellationToken = default)
    {
        var notificationMessage = await CreateMessageAsync(
            NotificationTargetType.Tenant, tenantId, type, title, message, category, data, cancellationToken);

        await FanOutToTenantAsync(tenantId, notificationMessage, cancellationToken);

        var payload = ToPayload(notificationMessage.Id, notificationMessage);
        await TryAsync(() => _realTimeNotifier.NotifyTenantAsync(tenantId, payload, cancellationToken), "real-time push to tenant");

        // FCM fan-out to every tenant member is intentionally not done inline here — pushing to
        // potentially hundreds of devices belongs in a background job, same reasoning as
        // NotificationConsts.MaxDailyNotificationsPerTenant for the campaign channel. Online recipients
        // still get it instantly via the SignalR push above; everyone else sees it next time they open
        // the feed (the fan-out rows written above).
    }

    public async Task PublishToAllAsync(
        UserNotificationType type, string title, string message,
        string? category = null, object? data = null, CancellationToken cancellationToken = default)
    {
        var notificationMessage = await CreateMessageAsync(
            NotificationTargetType.Broadcast, null, type, title, message, category, data, cancellationToken);

        using (_currentTenant.Change(null))
        {
            await FanOutToCurrentRealmAsync(null, notificationMessage, cancellationToken);
        }

        var tenants = await _tenantRepository.GetListAsync(cancellationToken: cancellationToken);
        foreach (var tenant in tenants)
        {
            await FanOutToTenantAsync(tenant.Id, notificationMessage, cancellationToken);
        }

        var payload = ToPayload(notificationMessage.Id, notificationMessage);
        await TryAsync(() => _realTimeNotifier.NotifyAllAsync(payload, cancellationToken), "real-time broadcast");
    }

    private async Task<NotificationMessage> CreateMessageAsync(
        NotificationTargetType targetType, Guid? sourceTenantId, UserNotificationType type, string title,
        string message, string? category, object? data, CancellationToken cancellationToken)
    {
        var notificationMessage = NotificationMessage.Create(
            _guidGenerator.Create(), targetType, sourceTenantId, type, title, message, category, Serialize(data));
        await _messageRepository.InsertAsync(notificationMessage, cancellationToken: cancellationToken);
        return notificationMessage;
    }

    private async Task FanOutToTenantAsync(Guid tenantId, NotificationMessage notificationMessage, CancellationToken cancellationToken)
    {
        using (_currentTenant.Change(tenantId))
        {
            await FanOutToCurrentRealmAsync(tenantId, notificationMessage, cancellationToken);
        }
    }

    // Must be called from inside the _currentTenant.Change(...) block matching realmTenantId, so both
    // the user lookup and the recipient-row insert land in the right realm.
    private async Task FanOutToCurrentRealmAsync(Guid? realmTenantId, NotificationMessage notificationMessage, CancellationToken cancellationToken)
    {
        var users = await _identityUserRepository.GetListAsync(cancellationToken: cancellationToken);
        if (users.Count == 0)
        {
            return;
        }

        var recipientRows = users
            .Select(u => UserNotification.Create(_guidGenerator.Create(), notificationMessage.Id, u.Id, realmTenantId))
            .ToList();

        await _userNotificationRepository.InsertManyAsync(recipientRows, cancellationToken: cancellationToken);
    }

    private async Task TryPushToDevicesAsync(Guid userId, NotificationMessage notificationMessage)
    {
        List<Device> devices;
        using (_currentTenant.Change(null)) // Device is Host-realm — same shape as NotificationSender.SendPushAsync
        {
            devices = await _deviceRepository.GetListAsync(d => d.CustomerId == userId && d.PushToken != null);
        }

        foreach (var device in devices)
        {
            await TryAsync(
                () => _pushSender.SendAsync(device.PushToken!, notificationMessage.Title, notificationMessage.Message),
                $"FCM push to device {device.Id}");
        }
    }

    private static RealTimeNotificationPayload ToPayload(Guid id, NotificationMessage notificationMessage)
    {
        return new RealTimeNotificationPayload
        {
            Id = id,
            Type = notificationMessage.Type,
            Title = notificationMessage.Title,
            Message = notificationMessage.Message,
            Category = notificationMessage.Category,
            Data = notificationMessage.Data,
            CreationTime = notificationMessage.CreationTime
        };
    }

    private static string? Serialize(object? data)
    {
        return data == null ? null : JsonSerializer.Serialize(data);
    }

    private async Task TryAsync(Func<Task> action, string description)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            // Delivery is best-effort by design — see the class-level comment. The DB row already
            // persisted above is the source of truth for "did this notification happen".
            Logger.LogWarning(ex, "Notification delivery step failed and was skipped: {Description}", description);
        }
    }
}
