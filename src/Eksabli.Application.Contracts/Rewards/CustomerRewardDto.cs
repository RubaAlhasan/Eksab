using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Rewards;

// Deliberately separate from RewardDto, which the Business Portal's own CRUD
// (IRewardAppService) also uses — BusinessName/AvailableBalance/CanAfford only make sense once a
// reward is seen in the context of "across every business I belong to", never for a single business
// managing its own catalog.
public class CustomerRewardDto : EntityDto<Guid>
{
    public Guid TenantId { get; set; }

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public RewardType Type { get; set; }

    public int PointsCost { get; set; }

    public int? StockRemaining { get; set; }

    public string? ImageBlobName { get; set; }

    // The business's own display name, so a feed spanning many businesses says where each reward is.
    public string BusinessName { get; set; } = string.Empty;

    // The caller's own available balance at THIS business (not the reward's own data) — carried so the
    // client can show "You have 40, need 10 more" without a second round trip per item.
    public int AvailableBalance { get; set; }

    public bool CanAfford { get; set; }

    // max(0, PointsCost - AvailableBalance) — 0 when CanAfford is true.
    public int PointsNeeded { get; set; }
}

public class CustomerRewardListDto
{
    public List<CustomerRewardDto> Items { get; set; } = new();
}
