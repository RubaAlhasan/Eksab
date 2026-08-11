using Volo.Abp.Application.Dtos;

namespace Eksabli.Platform;

public class AdminUserFilterDto : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public AdminUserType? Type { get; set; }
}
