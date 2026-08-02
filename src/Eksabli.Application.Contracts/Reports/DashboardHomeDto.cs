using System;
using System.Collections.Generic;

namespace Eksabli.Reports;

public class DashboardHomeDto
{
    // KPI definitions locked down per
    // docs/eksabli-loyalty-platform/features/07-business-dashboard/README.md#business-rules —
    // "active member" = Membership with >=1 Earn PointsTransaction in the trailing 30 days.
    public int ActiveMemberCount { get; set; }

    public int PointsIssuedLast30Days { get; set; }

    public int PointsRedeemedLast30Days { get; set; }

    public int ActiveCampaignCount { get; set; }

    public List<LowStockRewardDto> LowStockRewards { get; set; } = new();
}

public class LowStockRewardDto
{
    public Guid Id { get; set; }

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public int StockRemaining { get; set; }
}
