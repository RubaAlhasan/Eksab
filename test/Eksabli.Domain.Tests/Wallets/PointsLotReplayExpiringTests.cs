using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Eksabli.Wallets;

public class PointsLotReplayExpiringTests
{
    private static readonly DateTime Now = new(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowEnd = Now.AddDays(30);

    private static PointsTransaction Award(int points, DateTime? expiresAt) => PointsTransaction.Create(
        Guid.NewGuid(), Guid.NewGuid(), PointsTransactionType.Earn, points, PointsTransactionSource.Purchase, expiresAt: expiresAt);

    [Fact]
    public void Should_Count_Awards_Expiring_Inside_The_Window_and_Report_The_Earliest_Date()
    {
        var ledger = new List<PointsTransaction>
        {
            Award(40, Now.AddDays(20)),
            Award(25, Now.AddDays(5)),
        };

        var (points, earliest) = PointsLotReplay.ExpiringBetween(ledger, Now, WindowEnd, spendableCap: 1000);

        points.ShouldBe(65);
        earliest.ShouldBe(Now.AddDays(5));
    }

    [Fact]
    public void Should_Ignore_Awards_Expiring_After_The_Window_Or_Already_Expired()
    {
        var ledger = new List<PointsTransaction>
        {
            Award(100, Now.AddDays(45)), // beyond the window
            Award(70, Now.AddDays(-1)), // already past; the sweep owns it, not the warning
            Award(10, null), // never expires
        };

        var (points, earliest) = PointsLotReplay.ExpiringBetween(ledger, Now, WindowEnd, spendableCap: 1000);

        points.ShouldBe(0);
        earliest.ShouldBeNull();
    }

    [Fact]
    public void Should_Cap_The_Warning_At_What_Is_Spendable_Like_The_Sweep_Does()
    {
        var ledger = new List<PointsTransaction> { Award(100, Now.AddDays(3)) };

        var (points, earliest) = PointsLotReplay.ExpiringBetween(ledger, Now, WindowEnd, spendableCap: 30);

        points.ShouldBe(30);
        earliest.ShouldBe(Now.AddDays(3));
    }

    [Fact]
    public void Should_Report_Nothing_When_Nothing_Is_Spendable()
    {
        var ledger = new List<PointsTransaction> { Award(100, Now.AddDays(3)) };

        var (points, earliest) = PointsLotReplay.ExpiringBetween(ledger, Now, WindowEnd, spendableCap: 0);

        points.ShouldBe(0);
        earliest.ShouldBeNull();
    }
}
