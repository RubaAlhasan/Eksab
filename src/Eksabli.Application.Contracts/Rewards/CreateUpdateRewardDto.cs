using System;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.Rewards;

public class CreateUpdateRewardDto
{
    [Required]
    [StringLength(RewardConsts.MaxNameLength)]
    public string NameAr { get; set; } = string.Empty;

    [Required]
    [StringLength(RewardConsts.MaxNameLength)]
    public string NameEn { get; set; } = string.Empty;

    [Required]
    public RewardType Type { get; set; }

    [Range(1, int.MaxValue)]
    public int PointsCost { get; set; }

    public int? StockRemaining { get; set; }

    public DateTime? ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    [StringLength(RewardConsts.MaxImageBlobNameLength)]
    public string? ImageBlobName { get; set; }

    public int? ApprovalThresholdPoints { get; set; }
}
