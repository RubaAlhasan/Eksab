using System;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.Memberships;

public class JoinBusinessDto
{
    [Required]
    public Guid TenantId { get; set; }

    // The referrer's Membership.Id in this tenant, as handed out by IReferralAppService.GetMyReferralCodeAsync.
    // Optional — most joins aren't referred.
    public Guid? ReferralCode { get; set; }
}
