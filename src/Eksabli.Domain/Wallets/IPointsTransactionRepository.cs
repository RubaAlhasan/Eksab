using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Wallets;

public interface IPointsTransactionRepository : IRepository<PointsTransaction, Guid>
{
    // Business Portal > Transactions live ledger (ReportsAppService.GetTransactionsListAsync).
    // `createdByEmployeeIds`, when provided, is the caller's pre-resolved staff-id set for a Branch
    // filter — PointsTransaction has no branch column of its own (see the entity's own comment), so the
    // caller resolves BranchId -> EmployeeAssignment.UserId set first and passes it in here, keeping
    // that filter part of the same single database query as everything else rather than an in-memory
    // join. Same "filter/sort/page at the database" shape as ICouponRepository.GetListAsync — required
    // here specifically because PointsTransaction is an append-only ledger that grows forever, unlike
    // Coupon/Membership.
    Task<(List<PointsTransaction> Items, int TotalCount)> GetListAsync(
        PointsTransactionType? type = null,
        Guid? createdByEmployeeId = null,
        ICollection<Guid>? createdByEmployeeIds = null,
        DateTime? from = null,
        DateTime? to = null,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default);
}
