using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Branches;

public class BranchDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string? Phone { get; set; }

    public string? OpeningHoursJson { get; set; }
}
