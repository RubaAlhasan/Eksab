using System;

namespace Eksabli.Reports;

[Serializable]
public class TransactionsExcelDownloadTokenCacheItem
{
    public string Token { get; set; } = string.Empty;
}
