using System;

namespace Eksabli.Reports;

// Backs the "Smart deal sales" Excel export. The same filters as the table, so the file matches the screen. The
// download token comes from GetSmartDealSalesDownloadTokenAsync and is valid for 30 seconds.
public class SmartDealSalesExcelDownloadDto
{
    public string DownloadToken { get; set; } = string.Empty;

    public Guid? BranchId { get; set; }

    public Guid? StaffId { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    // The same search as the table, so the file matches what is on screen.
    public string? Search { get; set; }
}
