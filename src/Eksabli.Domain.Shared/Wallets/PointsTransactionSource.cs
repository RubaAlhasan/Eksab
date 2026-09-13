namespace Eksabli.Wallets;

public enum PointsTransactionSource
{
    Purchase = 0,
    Campaign = 1,
    Referral = 2,
    Birthday = 3,
    Manual = 4,
    Reward = 5,

    // The extra points a customer's loyalty tier earned them, beyond what the plain base rate/Purchase
    // row already gives — split out by PosAppService.AwardPointsCoreAsync the same way a Campaign's
    // contribution is, ReferenceId pointing at the Tier that produced it. See that method's own comment.
    Tier = 6
}
