using System;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.Rewards;

public class RedeemRewardDto
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid RewardId { get; set; }
}
