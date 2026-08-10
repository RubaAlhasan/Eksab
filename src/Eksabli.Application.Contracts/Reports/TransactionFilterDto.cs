using System;
using Eksabli.Wallets;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Reports;

// Backs the Business Portal > Transactions live ledger (GetTransactionsListAsync). Unlike
// TransactionsExcelDownloadDto (From/To only), this carries the full filter set the prototype's
// transactions.html table exposes. BranchId isn't a real column on PointsTransaction — see
// ReportsAppService.GetTransactionsListAsync for how it's derived from CreatedByEmployeeId.
public class TransactionFilterDto : PagedAndSortedResultRequestDto
{
    public PointsTransactionType? Type { get; set; }

    public Guid? BranchId { get; set; }

    public Guid? StaffId { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }
}
