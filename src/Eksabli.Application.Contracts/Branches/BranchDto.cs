using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Branches;

public class BranchDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string? Phone { get; set; }

    // Not a Branch property — deserialized from Branch.OpeningHoursJson by BranchAppService after
    // mapping (BranchOpeningHoursMapper.Deserialize), same pattern as CategoryDto.BusinessCount.
    public List<DayOpeningHoursDto> OpeningHours { get; set; } = new();
}
