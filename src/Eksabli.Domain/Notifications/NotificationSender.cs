using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eksabli.Devices;
using Eksabli.Features;
using Eksabli.Memberships;
using Eksabli.Sms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Features;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Notifications;

// Resolves one Notification's recipient contact info and hands it to the right channel adapter.
// Crosses from the tenant-realm Membership to the Host-realm Device/IdentityUser — same realm-crossing
// shape as PosAppService.LookupCustomerByPhoneAsync.
public class NotificationSender : INotificationSender, ITransientDependency
{
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<Device, Guid> _deviceRepository;
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly IPushNotificationSender _pushSender;
    private readonly IEmailSender _emailSender;
    private readonly ISmsSender _smsSender;
    private readonly IFeatureChecker _featureChecker;
    private readonly ICurrentTenant _currentTenant;

    public ILogger<NotificationSender> Logger { get; set; } = NullLogger<NotificationSender>.Instance;

    public NotificationSender(
        IRepository<Membership, Guid> membershipRepository,
        IRepository<Device, Guid> deviceRepository,
        IIdentityUserRepository identityUserRepository,
        IPushNotificationSender pushSender,
        IEmailSender emailSender,
        ISmsSender smsSender,
        IFeatureChecker featureChecker,
        ICurrentTenant currentTenant)
    {
        _membershipRepository = membershipRepository;
        _deviceRepository = deviceRepository;
        _identityUserRepository = identityUserRepository;
        _pushSender = pushSender;
        _emailSender = emailSender;
        _smsSender = smsSender;
        _featureChecker = featureChecker;
        _currentTenant = currentTenant;
    }

    public async Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        if (!await IsChannelAllowedAsync(notification.Channel))
        {
            Logger.LogWarning(
                "Notification {NotificationId} skipped — {Channel} isn't enabled on this tenant's plan.",
                notification.Id, notification.Channel);
            notification.MarkFailed();
            return;
        }

        if (notification.MembershipId == null)
        {
            // Broadcasts aren't fanned out to individual channels — reserved for future in-app/
            // announcement use, not push/email/SMS delivery.
            notification.MarkFailed();
            return;
        }

        var membership = await _membershipRepository.GetAsync(notification.MembershipId.Value);

        var succeeded = notification.Channel switch
        {
            NotificationChannel.Push => await SendPushAsync(membership.CustomerId, notification),
            NotificationChannel.Email => await SendEmailAsync(membership.CustomerId, notification),
            NotificationChannel.Sms => await SendSmsAsync(membership.CustomerId, notification),
            NotificationChannel.InApp => true, // the Notification row itself IS the in-app delivery
            _ => false
        };

        if (succeeded)
        {
            notification.MarkSent(DateTime.UtcNow);
        }
        else
        {
            notification.MarkFailed();
        }
    }

    private async Task<bool> IsChannelAllowedAsync(NotificationChannel channel)
    {
        return channel switch
        {
            NotificationChannel.Sms => await _featureChecker.IsEnabledAsync(EksabliFeatures.SmsNotifications),
            NotificationChannel.Push => await _featureChecker.IsEnabledAsync(EksabliFeatures.PushNotifications),
            _ => true // Email and InApp aren't plan-gated
        };
    }

    private async Task<bool> SendPushAsync(Guid customerId, Notification notification)
    {
        List<Device> devices;
        using (_currentTenant.Change(null)) // Device is Host-realm
        {
            devices = await _deviceRepository.GetListAsync(d => d.CustomerId == customerId && d.PushToken != null);
        }

        if (devices.Count == 0)
        {
            return false;
        }

        foreach (var device in devices)
        {
            await _pushSender.SendAsync(device.PushToken!, notification.Title, notification.Body);
        }

        return true;
    }

    private async Task<bool> SendEmailAsync(Guid customerId, Notification notification)
    {
        IdentityUser? user;
        using (_currentTenant.Change(null)) // IdentityUser for a customer lives in the Host realm
        {
            user = await _identityUserRepository.FindAsync(customerId);
        }

        if (string.IsNullOrWhiteSpace(user?.Email))
        {
            return false;
        }

        await _emailSender.SendAsync(user.Email, notification.Title, notification.Body);
        return true;
    }

    private async Task<bool> SendSmsAsync(Guid customerId, Notification notification)
    {
        IdentityUser? user;
        using (_currentTenant.Change(null))
        {
            user = await _identityUserRepository.FindAsync(customerId);
        }

        if (string.IsNullOrWhiteSpace(user?.PhoneNumber))
        {
            return false;
        }

        await _smsSender.SendAsync(user.PhoneNumber, notification.Body);
        return true;
    }
}
