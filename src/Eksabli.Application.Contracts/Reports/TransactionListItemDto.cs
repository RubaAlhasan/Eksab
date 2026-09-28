using System;
using System.Collections.Generic;
using Eksabli.Wallets;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Reports;

// One row of the Business Portal > Transactions live ledger table — one real-world *process*, not
// necessarily one raw PointsTransaction. A single POS purchase can insert up to four ledger rows (base
// Purchase + Tier bonus + per-campaign bonus, all sharing PointsTransaction.BatchId), which
// ReportsAppService.GetTransactionsListAsync groups back into one row here so staff see one checkout,
// not several unrelated-looking entries with the same timestamp. Customer/Branch/Staff are all resolved
// server-side since PointsTransaction itself only carries WalletId/CreatedByEmployeeId soft references.
public class TransactionListItemDto : EntityDto<Guid>
{
    public Guid? CustomerId { get; set; }

    public string? CustomerFirstName { get; set; }

    public string? CustomerLastName { get; set; }

    // Shared by every component (a batch is always all-Earn — see PosAppService.AwardPointsCoreAsync,
    // the only place rows ever batch together).
    public PointsTransactionType Type { get; set; }

    // Net delta across every component — see Components for the per-row breakdown.
    public int Points { get; set; }

    // The badge shown on the collapsed row: the base Purchase component's source when there is one,
    // otherwise the (always single, for non-batched processes) component's own source.
    public PointsTransactionSource Source { get; set; }

    // Derived from CreatedByEmployeeId -> EmployeeAssignment.BranchId — null when the row has no staff
    // attribution (customer/system-triggered) or the staff member has all-branch access.
    public Guid? BranchId { get; set; }

    public Guid? StaffId { get; set; }

    public string? BranchName { get; set; }

    // No display name exists for staff anywhere in this codebase (see EmployeeAssignmentAppService) —
    // email is the real, resolvable identifier.
    public string? StaffEmail { get; set; }

    public DateTime CreationTime { get; set; }

    // One entry per raw PointsTransaction this process produced — almost always 1, up to 4 for a single
    // purchase that also earned a tier and/or campaign bonus. What the Transactions table's "Details"
    // action expands into.
    public List<TransactionComponentDto> Components { get; set; } = new();
}
