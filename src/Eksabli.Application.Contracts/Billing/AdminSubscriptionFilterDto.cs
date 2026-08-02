using Volo.Abp.Application.Dtos;

namespace Eksabli.Billing;

public class AdminSubscriptionFilterDto : PagedAndSortedResultRequestDto
{
    public TenantSubscriptionStatus? Status { get; set; }
}
