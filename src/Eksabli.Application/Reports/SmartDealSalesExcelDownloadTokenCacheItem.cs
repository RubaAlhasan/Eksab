using System;

namespace Eksabli.Reports;

[Serializable]
public class SmartDealSalesExcelDownloadTokenCacheItem
{
    public string Token { get; set; } = string.Empty;

    // The business that asked for the file. The file endpoint is anonymous, so the token must carry the tenant: otherwise
    // a token minted for one business could be redeemed against another's data.
    public Guid? TenantId { get; set; }
}
