using System;
using System.ComponentModel.DataAnnotations;
using Eksabli.Engagement;

namespace Eksabli.Memberships;

public class JoinBusinessDto
{
    [Required]
    public Guid TenantId { get; set; }

    // A short code handed out by IReferralAppService.GetMyReferralCodeAsync (see
    // Membership.ReferralCode's own comment) — NOT the referrer's Membership.Id anymore. Optional —
    // most joins aren't referred.
    [StringLength(ReferralConsts.CodeLength)]
    public string? ReferralCode { get; set; }
}
