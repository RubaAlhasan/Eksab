namespace Eksabli.Engagement;

public enum ReferralStatus
{
    // Referee has joined the business via the referrer's code.
    Pending = 0,

    // Referee completed the qualifying action (their first points-earning transaction).
    Completed = 1,

    // Bonus points have been paid to both referrer and referee.
    Rewarded = 2
}
