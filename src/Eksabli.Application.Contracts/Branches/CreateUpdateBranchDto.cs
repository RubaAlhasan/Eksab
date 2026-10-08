using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.Branches;

public class CreateUpdateBranchDto
{
    [Required]
    [StringLength(BranchConsts.MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    [StringLength(BranchConsts.MaxAddressLength)]
    public string? Address { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    [StringLength(BranchConsts.MaxPhoneLength)]
    public string? Phone { get; set; }

    // At most one entry per day of week (BranchOpeningHoursMapper.Serialize enforces this and does the
    // actual HH:mm validation) — null/empty means no hours set, same as before this was structured.
    public List<DayOpeningHoursDto>? OpeningHours { get; set; }
}
