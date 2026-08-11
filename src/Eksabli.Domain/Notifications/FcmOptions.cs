namespace Eksabli.Notifications;

// Bound from the "Fcm" configuration section (appsettings.json / environment / secret manager — never
// commit the credentials file itself). Left unset in dev by default, same "no provider chosen yet until
// configured" posture as the other Null* senders — see EksabliDomainModule for the swap.
public class FcmOptions
{
    /// <summary>
    /// Path to the Firebase service-account JSON credentials file (Google Cloud Console → Project
    /// Settings → Service Accounts → Generate new private key).
    /// </summary>
    public string? CredentialsFilePath { get; set; }
}
