using System;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.Memberships;

public class JoinBusinessDto
{
    [Required]
    public Guid TenantId { get; set; }
}
