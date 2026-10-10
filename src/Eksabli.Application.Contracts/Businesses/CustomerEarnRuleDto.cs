using Eksabli.Shared;
using Eksabli.Wallets;

namespace Eksabli.Businesses;

// One way a business awards points, shown to customers so they know how to earn. Deliberately only the rule itself:
// the tier multiplier and campaign multipliers stay internal, same reasoning as PointsWalletDto's tier progress.
public class CustomerEarnRuleDto
{
    public PointRuleType RuleType { get; set; }

    public decimal PointsPerUnit { get; set; }

    // Set for PerCurrencyUnit ("N points per 1 USD spent"), null for PerVisit.
    public Currency? Currency { get; set; }
}
