namespace Eksabli.Engagement;

public class ReferralCodeDto
{
    // A short, human-shareable code (see Membership.ReferralCode's own comment for the shape) —
    // pass back as JoinBusinessDto.ReferralCode. Previously the referrer's raw Membership.Id (a GUID);
    // that worked functionally but was never something a person could actually read out, type, or
    // share in a text message.
    public string Code { get; set; } = string.Empty;
}
