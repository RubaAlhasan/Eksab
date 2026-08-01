using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Rewards;

public class CouponDto : AuditedEntityDto<Guid>
{
    public Guid RewardId { get; set; }

    public string? RewardNameAr { get; set; }

    public string? RewardNameEn { get; set; }

    public Guid MembershipId { get; set; }

    public Guid? TenantId { get; set; }

    public string Code { get; set; } = string.Empty;

    public CouponStatus Status { get; set; }

    public DateTime IssuedAt { get; set; }

    public DateTime? RedeemedAt { get; set; }

    public Guid? RedeemedByEmployeeId { get; set; }

    public Guid? RedeemedBranchId { get; set; }
}
