using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Billing;

public class AdminSubscriptionFilterDto : PagedAndSortedResultRequestDto
{
    public TenantSubscriptionStatus? Status { get; set; }

    // Scopes the list to a single tenant's subscription — added for Admin Portal > Business Details'
    // Billing tab (angular/src/app/admin/businesses/admin-business-details.component.ts), which needs
    // "this one tenant's subscription" without paging through every platform subscription client-side.
    public Guid? TenantId { get; set; }
}
