namespace Eksabli.Memberships;

public class WalletQrTokenResultDto
{
    public string Token { get; set; } = string.Empty;

    public int ExpiresInSeconds { get; set; }
}
