using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Rewards;

public interface ICouponRepository : IRepository<Coupon, Guid>
{
    // Staff-facing Coupons audit trail — filterable by status/branch, per the dashboard screen.
    Task<(List<Coupon> Items, int TotalCount)> GetListAsync(
        CouponStatus? status = null,
        Guid? branchId = null,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default);
}
