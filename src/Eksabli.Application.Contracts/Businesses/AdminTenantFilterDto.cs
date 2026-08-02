using Eksabli.BusinessProfiles;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Businesses;

public class AdminTenantFilterDto : PagedAndSortedResultRequestDto
{
    public TenantApprovalStatus? ApprovalStatus { get; set; }

    public string? FilterText { get; set; }
}
