namespace Eksabli.Dashboards;

// The windows and thresholds behind the Dashboard 360 pages (Business and Admin) and the existing Reports. Kept in one
// place so the two dashboards can't drift apart on what "active" or "low stock" means. These are tuning values for
// what counts as noteworthy, not money rules.
public static class DashboardDefinitions
{
    // "Active member" (Business) and "active business" (Admin): at least one Earn transaction in this many trailing days.
    public const int ActivityWindowDays = 30;

    // A live offer that has taken no order in this many days gets an insight.
    public const int InactiveOfferWindowDays = 7;

    // Rewards at or below this many units left are flagged as running low.
    public const int LowStockThreshold = 10;

    // A pending Buy Now order whose hold ends within this many minutes gets a warning.
    public const int BuyNowExpiryWarningMinutes = 15;

    // Below this share of purchases carrying an amount, the recorded-value totals are flagged as incomplete.
    public const int LowValueCoveragePercent = 50;

    // Coverage is only judged once there are enough purchases for the percentage to mean something.
    public const int MinPurchasesForCoverageInsight = 10;

    // Default window when a caller doesn't pass one, and the longest window a single request may ask for. The longest
    // bound keeps the hourly aggregation to a few thousand buckets.
    public const int DefaultRangeDays = 30;
    public const int MaxRangeDays = 400;

    // Length of the "top businesses" and "top offers" lists.
    public const int TopListSize = 10;

    // How many of the busiest hours the uncovered-peak insight looks at.
    public const int PeakInsightHours = 3;
}
