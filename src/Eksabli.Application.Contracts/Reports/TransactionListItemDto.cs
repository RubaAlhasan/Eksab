using System;
using Eksabli.Wallets;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Reports;

// One row of the Business Portal > Transactions live ledger table. Customer/Branch/Staff are all
// resolved server-side (see ReportsAppService.GetTransactionsListAsync) since PointsTransaction itself
// only carries WalletId/CreatedByEmployeeId soft references — plain ids only, no names, matching the
// same "resolve ids, not names, let the DTO be cheap" shape TransactionExcelDto/CouponDto use elsewhere.
public class TransactionListItemDto : EntityDto<Guid>
{
    public Guid? CustomerId { get; set; }

    public string? CustomerFirstName { get; set; }

    public string? CustomerLastName { get; set; }

    public PointsTransactionType Type { get; set; }

    public int Points { get; set; }

    public PointsTransactionSource Source { get; set; }

    // Derived from CreatedByEmployeeId -> EmployeeAssignment.BranchId — null when the row has no staff
    // attribution (customer/system-triggered) or the staff member has all-branch access.
    public Guid? BranchId { get; set; }

    public Guid? StaffId { get; set; }

    public DateTime CreationTime { get; set; }
}
