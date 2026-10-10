using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Reports;

// Backs the "Smart deal sales" tab (GetSmartDealSalesAsync). Sorting is fixed server-side (newest completed sale first),
// so the Sorting field from the base class is ignored on purpose.
public class SmartDealSaleFilterDto : PagedAndSortedResultRequestDto
{
    public Guid? BranchId { get; set; }

    public Guid? StaffId { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    // A full Deal ID (matches every sale of that deal) or a sale code (matches that one sale). Empty means no search.
    public string? Search { get; set; }

    // One customer's sales at this business: the Business Portal customer page's "Smart deal sales" tab. Set by the
    // membership endpoint from the route, not by the caller.
    public Guid? MembershipId { get; set; }
}
