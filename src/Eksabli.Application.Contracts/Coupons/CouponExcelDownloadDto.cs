using System;

namespace Eksabli.Rewards;

public class CouponExcelDownloadDto
{
    public string DownloadToken { get; set; } = string.Empty;

    public CouponStatus? Status { get; set; }

    public Guid? BranchId { get; set; }

    public string? Sorting { get; set; }
}
