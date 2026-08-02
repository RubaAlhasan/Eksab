using System;

namespace Eksabli.Engagement;

public class ReferralCodeDto
{
    // The referrer's Membership.Id in the requested tenant — pass back as JoinBusinessDto.ReferralCode.
    public Guid Code { get; set; }
}
