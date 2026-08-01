using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Rewards;

public class RewardDto : FullAuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public RewardType Type { get; set; }

    public int PointsCost { get; set; }

    public int? StockRemaining { get; set; }

    public DateTime? ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    public string? ImageBlobName { get; set; }

    public int? ApprovalThresholdPoints { get; set; }
}
