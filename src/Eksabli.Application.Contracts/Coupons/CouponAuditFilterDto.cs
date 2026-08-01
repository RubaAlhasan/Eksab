using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Rewards;

public class CouponAuditFilterDto : PagedAndSortedResultRequestDto
{
    public CouponStatus? Status { get; set; }

    public Guid? BranchId { get; set; }
}
