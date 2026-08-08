using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Memberships;

public class MemberFilterDto : PagedAndSortedResultRequestDto
{
    // Matches against name (first/last) or phone number.
    public string? FilterText { get; set; }

    public Guid? TierId { get; set; }

    public MembershipStatus? Status { get; set; }
}
