using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.CustomerProfiles;

public class CustomerProfileDto : AuditedEntityDto<Guid>
{
    public Guid UserId { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public CustomerGender Gender { get; set; }
}
