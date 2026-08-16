using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Rewards;

public class CouponAuditFilterDto : PagedAndSortedResultRequestDto
{
    public CouponStatus? Status { get; set; }

    public Guid? BranchId { get; set; }

    // Customer Details page's "Coupons Redeemed" tab — Coupon.MembershipId is a real column
    // (the member it was issued to, not just who redeemed it).
    public Guid? MembershipId { get; set; }
}
