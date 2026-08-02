using System;

namespace Eksabli.Reports;

public class TransactionsExcelDownloadDto
{
    public string DownloadToken { get; set; } = string.Empty;

    public DateTime From { get; set; }

    public DateTime To { get; set; }
}
