using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using FcmNotification = FirebaseAdmin.Messaging.Notification; // Notification (this namespace) is the campaign delivery-record entity — alias to avoid the clash.

namespace Eksabli.Notifications;

// The real IPushNotificationSender — the "FCM -> Flutter Mobile" leg of the unified notification flow.
// Wired in over NullPushNotificationSender by EksabliDomainModule only once Fcm:CredentialsFilePath is
// configured; every existing caller (NotificationSender for campaigns, NotificationPublisher for the
// hub) already goes through IPushNotificationSender, so nothing else needed to change to pick this up.
// Registered as a singleton because the underlying FirebaseApp is meant to be created exactly once per
// process (the Admin SDK throws if you try to create a second default-named app).
public class FirebaseCloudMessagingSender : IPushNotificationSender
{
    private readonly Lazy<FirebaseApp> _app;

    public ILogger<FirebaseCloudMessagingSender> Logger { get; set; } = NullLogger<FirebaseCloudMessagingSender>.Instance;

    public FirebaseCloudMessagingSender(IOptions<FcmOptions> options)
    {
        _app = new Lazy<FirebaseApp>(() => FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.FromFile(options.Value.CredentialsFilePath)
        }));
    }

    public async Task SendAsync(string pushToken, string title, string body)
    {
        var message = new Message
        {
            Token = pushToken,
            Notification = new FcmNotification { Title = title, Body = body },
            // Flutter background-handler routing data — mirrors the Category/Data shape UserNotification
            // carries for the web client, so both platforms can deep-link off the same payload keys.
            Data = new Dictionary<string, string> { ["title"] = title, ["body"] = body }
        };

        try
        {
            await FirebaseMessaging.GetMessaging(_app.Value).SendAsync(message);
        }
        catch (FirebaseMessagingException ex) when (
            ex.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
        {
            // Stale/uninstalled-app token — not a transient failure, and not this call's job to prune
            // Device.PushToken (that's the Devices feature's concern). Log and move on.
            Logger.LogWarning(ex, "FCM rejected push token {PushToken} ({ErrorCode}) — likely stale.", pushToken, ex.MessagingErrorCode);
        }
    }
}
