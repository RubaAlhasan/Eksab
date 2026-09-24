namespace Eksabli.Businesses;

public class ImpersonationTokenResultDto
{
    // Single-use, short-lived — exchanged at POST /connect/token as grant_type=impersonation's own
    // "code" parameter (see TenantImpersonationGrantHandler). Never a real access token itself.
    public string Code { get; set; } = string.Empty;

    public int ExpiresInSeconds { get; set; }
}
