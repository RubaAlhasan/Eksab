using System;
using System.Collections.Generic;
using System.Linq;

namespace Eksabli.Wallets;

// Works out how many points each awarded row still holds, from the ledger alone. Ledger rows are immutable and the
// wallet keeps only totals, so this is the one place that knows which points were spent or expired, and from which award.
//
// Spending is first-in, first-out by award time: a debit takes the oldest points still held, so the points that are
// about to expire are the ones a customer has not yet spent. Expiry is aimed at one award: an Expire row names the
// award it removes points from (ReferenceId), so replaying it never touches a different award.
public static class PointsLotReplay
{
    public sealed class Lot
    {
        public Guid Id { get; init; }

        public DateTime CreationTime { get; init; }

        public DateTime? ExpiresAt { get; init; }

        public PointsTransactionSource Source { get; init; }

        /// <summary>Points from this award that have not been spent or expired yet.</summary>
        public int Remaining { get; internal set; }
    }

    /// <summary>The awards in the ledger, oldest first, each with its remaining points after every spend and expiry so far.</summary>
    public static IReadOnlyList<Lot> Replay(IEnumerable<PointsTransaction> ledger)
    {
        var lots = new List<Lot>();

        foreach (var entry in ledger.OrderBy(t => t.CreationTime).ThenBy(t => t.Id))
        {
            if (entry.Type == PointsTransactionType.Expire)
            {
                // Expire rows are negative and name the award they came out of.
                var expiredLot = lots.FirstOrDefault(l => l.Id == entry.ReferenceId);
                if (expiredLot != null)
                {
                    expiredLot.Remaining = Math.Max(0, expiredLot.Remaining + entry.Points);
                }
            }
            else if (entry.Points > 0)
            {
                lots.Add(new Lot
                {
                    Id = entry.Id,
                    CreationTime = entry.CreationTime,
                    ExpiresAt = entry.ExpiresAt,
                    Source = entry.Source,
                    Remaining = entry.Points,
                });
            }
            else if (entry.Points < 0)
            {
                var toSpend = -entry.Points;
                foreach (var lot in lots)
                {
                    if (toSpend == 0) break;

                    var taken = Math.Min(lot.Remaining, toSpend);
                    lot.Remaining -= taken;
                    toSpend -= taken;
                }
            }
        }

        return lots;
    }

    /// <summary>
    /// The points that expire in (fromUtc, toUtc], capped at <paramref name="spendableCap"/> — the same cap the
    /// expiry sweep applies, so what a customer is warned about is what will actually leave their balance — and the
    /// earliest of those expiry dates. Zero points means no expiry date.
    /// </summary>
    public static (int Points, DateTime? EarliestExpiry) ExpiringBetween(
        IEnumerable<PointsTransaction> ledger, DateTime fromUtc, DateTime toUtc, int spendableCap)
    {
        var expiring = Replay(ledger)
            .Where(lot => lot.ExpiresAt is { } expiresAt && expiresAt > fromUtc && expiresAt <= toUtc && lot.Remaining > 0)
            .ToList();

        var points = Math.Min(expiring.Sum(lot => lot.Remaining), Math.Max(0, spendableCap));
        if (points == 0)
        {
            return (0, null);
        }

        return (points, expiring.Min(lot => lot.ExpiresAt));
    }
}
