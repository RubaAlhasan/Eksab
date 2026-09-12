import 'package:eksabli_mobile/shared/models/models.dart';
import 'package:flutter_test/flutter_test.dart';

/// Tier progress is the app's main reason to come back, so the arithmetic behind the bar is worth
/// pinning down — particularly that it measures from the CURRENT tier's floor rather than from zero.
void main() {
  Membership at(int lifetimeEarned, {int? floor = 2000, String? next = 'Platinum', int? nextAt = 5000}) =>
      Membership.fromWalletJson({
        'tenantId': 'tenant-1',
        'membershipId': 'mem-1',
        'balance': 400,
        'lifetimeEarned': lifetimeEarned,
        'lifetimeRedeemed': 0,
        'currentTierName': 'Gold',
        'currentTierMinLifetimePoints': floor,
        'nextTierName': next,
        'nextTierMinLifetimePoints': nextAt,
      });

  test('measures progress from the current tier floor, not from zero', () {
    // 3,400 lifetime is 1,400 into the 3,000-point stretch from Gold to Platinum. Measured from zero
    // it would read 68% and tell someone who just reached Gold they were nearly Platinum.
    expect(at(3400).tierProgress, closeTo(1400 / 3000, 0.0001));
  });

  test('is empty at the moment a tier is reached', () {
    expect(at(2000).tierProgress, 0);
  });

  test('reports the points still to earn', () {
    expect(at(3400).pointsToNextTier, 1600);
  });

  test('clamps rather than overflowing once the target is passed', () {
    // Reachable in practice: the tier cache can lag a big award, and a merchant can lower a
    // threshold under customers who are already above it.
    expect(at(6000).tierProgress, 1);
    expect(at(6000).pointsToNextTier, 0);
  });

  test('has no progress on the top tier', () {
    // Nothing left to climb — the widget hides itself rather than drawing a full bar with no target.
    final top = at(9000, next: null, nextAt: null);
    expect(top.hasTierProgress, isFalse);
    expect(top.pointsToNextTier, 0);
  });

  test('has no progress when the business defines no tiers', () {
    expect(at(3400, floor: null, next: null, nextAt: null).hasTierProgress, isFalse);
  });

  test('treats a missing floor as zero rather than crashing', () {
    expect(at(1000, floor: null).tierProgress, closeTo(1000 / 5000, 0.0001));
  });
}
