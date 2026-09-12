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

    public int PointsCost { get; set; }

    public DateTime IssuedAt { get; set; }

    // Drives the customer app's countdown while a redemption is Pending. Null on legacy `Issued` rows.
    public DateTime? ReservationExpiresAt { get; set; }

    public DateTime? RedeemedAt { get; set; }

    public Guid? RedeemedByEmployeeId { get; set; }

    public Guid? RedeemedBranchId { get; set; }

    // Shown to the customer verbatim when staff decline — "why did this fail?" is the first thing they
    // will ask, and an unexplained Cancelled is worse than no status at all.
    public string? RejectionReason { get; set; }
}
