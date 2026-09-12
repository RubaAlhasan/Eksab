import 'package:eksabli_mobile/core/api/eksabli_api.dart';
import 'package:eksabli_mobile/core/auth/auth_exception.dart';
import 'package:eksabli_mobile/shared/models/models.dart';
import 'package:eksabli_mobile/shared/providers/app_providers.dart';

/// Test doubles.
///
/// The app now talks to a real API, so widget tests need a stand-in rather than
/// the fixture data they used to read directly. [FakeApi] returns a small,
/// fixed dataset; screens are exercised against it exactly as they would be
/// against the server.

const testCustomer = Customer(
  id: 'cus-1',
  firstName: 'Layla',
  lastName: 'Haddad',
  initials: 'LH',
  email: 'layla.haddad@example.com',
  phone: '+971501234567',
);

/// A session that is already signed in, so screens can be pumped without
/// standing up the auth server.
class SignedInSession extends SessionNotifier {
  @override
  Future<Customer?> build() async => testCustomer;
}

final _coffee = Business.fromJson(const {
  'tenantId': 'tenant-1',
  'name': 'Cedar & Bean Coffee',
  'categoryNameEn': 'Coffee Shop',
  'branchCount': 6,
  'businessProfileId': 'profile-1',
  'hasLogo': false,
});

final _fitness = Business.fromJson(const {
  'tenantId': 'tenant-2',
  'name': 'Pulse Fitness Club',
  'categoryNameEn': 'Fitness & Gym',
  'branchCount': 3,
  'businessProfileId': 'profile-2',
  'hasLogo': false,
});

/// Implements the same surface as [EksabliApi] without any HTTP.
///
/// Not a subclass: [EksabliApi] holds a Dio instance, and the point of the fake
/// is that no client exists. Tests cast it in via the provider override.
class FakeApi implements EksabliApi {
  /// Pending redemptions by coupon id. Exposed so a test can flip one to approved/declined the way
  /// staff would, then let the screen's poll pick it up.
  final pendingCoupons = <String, Coupon>{};
  Map<String, Coupon> get _pending => pendingCoupons;

  /// Set to make the next [redeemReward] fail the way the server does — "out of stock", "not enough
  /// points", and so on all arrive as an [AuthException] carrying the server's own message.
  String? failRedeemWith;

  /// Stands in for a staff member acting in the Business Portal: moves the open coupon to its
  /// settled status so a polling screen picks the outcome up.
  void settlePending(CouponStatus status, {String? reason}) {
    for (final entry in pendingCoupons.entries.toList()) {
      final c = entry.value;
      pendingCoupons[entry.key] = Coupon(
        id: c.id,
        rewardId: c.rewardId,
        businessId: c.businessId,
        code: c.code,
        status: status,
        issuedAt: c.issuedAt,
        pointsCost: c.pointsCost,
        rewardName: c.rewardName,
        rejectionReason: reason,
      );
    }
  }

  final _notifications = <AppNotification>[
    AppNotification.fromJson(const {
      'id': 'n1',
      'type': 0,
      'title': 'Double points this weekend',
      'message': 'Earn 2x points on every order.',
      'isRead': false,
      'creationTime': '2026-08-04T08:00:00',
    }),
    AppNotification.fromJson(const {
      'id': 'n2',
      'type': 1,
      'title': 'Coupon redeemed',
      'message': 'Enjoy your free latte.',
      'isRead': true,
      'creationTime': '2026-07-29T08:02:00',
    }),
  ];

  @override
  Future<List<Business>> searchBusinesses({
    String? query,
    String? categoryId,
    double? latitude,
    double? longitude,
    int skip = 0,
    int max = 50,
  }) async {
    final all = [_coffee, _fitness];
    if (query == null || query.isEmpty) return all;
    final q = query.toLowerCase();
    return all
        .where(
          (b) =>
              b.name.toLowerCase().contains(q) ||
              b.category.toLowerCase().contains(q),
        )
        .toList();
  }

  @override
  Future<Business> getBusiness(String tenantId) async =>
      tenantId == _fitness.id ? _fitness : _coffee;

  @override
  Future<Map<String, Business>> lookupBusinesses(List<String> tenantIds) async {
    final all = {_coffee.id: _coffee, _fitness.id: _fitness};
    return {
      for (final id in tenantIds)
        if (all[id] != null) id: all[id]!,
    };
  }

  @override
  Future<List<Membership>> myMemberships() async => [
    Membership.fromWalletJson(const {
      'membershipId': 'mem-1',
      'tenantId': 'tenant-1',
      'balance': 1250,
      'lifetimeEarned': 3400,
      'lifetimeRedeemed': 2150,
      'currentTierName': 'Gold',
    }),
    Membership.fromWalletJson(const {
      'membershipId': 'mem-2',
      'tenantId': 'tenant-2',
      'balance': 420,
      'lifetimeEarned': 920,
      'lifetimeRedeemed': 500,
      'currentTierName': 'Silver',
    }),
  ];

  @override
  Future<void> joinBusiness(String tenantId) async {}

  @override
  Future<String> walletQrToken() async => 'test-token';

  @override
  Future<List<PointTransaction>> transactions(
    String tenantId, {
    int skip = 0,
    int max = 100,
  }) async => [
    PointTransaction.fromJson(const {
      'id': 't1',
      'type': 0,
      'points': 45,
      'source': 0,
      'creationTime': '2026-08-04T09:15:00',
    }, tenantId),
  ];

  @override
  Future<List<Reward>> rewardCatalog(String tenantId) async => [
    Reward.fromJson(const {
      'id': 'rew-1',
      'tenantId': 'tenant-1',
      'nameEn': 'Free Large Latte',
      'type': 1,
      'pointsCost': 500,
      'stockRemaining': 42,
    }),
  ];

  /// Mirrors the real service: redemption OPENS a pending coupon (status 4), it does not complete
  /// one. A fake that returned `redeemed` would let a screen pass tests it would fail in production.
  @override
  Future<Coupon> redeemReward({
    required String tenantId,
    required String rewardId,
  }) async {
    final failure = failRedeemWith;
    if (failure != null) {
      failRedeemWith = null;
      throw AuthException(failure);
    }

    final coupon = Coupon.fromJson({
      'id': 'cpn-new',
      'rewardId': rewardId,
      'tenantId': tenantId,
      'code': '3B02543F',
      'status': 4,
      'pointsCost': 500,
      'issuedAt': DateTime.now().toIso8601String(),
      'reservationExpiresAt': DateTime.now()
          .toUtc()
          .add(const Duration(minutes: 15))
          .toIso8601String(),
    });
    _pending[coupon.id] = coupon;
    return coupon;
  }

  /// Whatever the test last put in [_pending] — tests drive an approval or decline by writing here,
  /// standing in for a staff member acting in the Business Portal.
  @override
  Future<Coupon> coupon({
    required String tenantId,
    required String couponId,
  }) async => _pending[couponId] ?? (throw StateError('no coupon $couponId'));

  @override
  Future<Coupon> cancelCoupon({
    required String tenantId,
    required String couponId,
  }) async {
    final existing = _pending[couponId]!;
    final cancelled = Coupon.fromJson({
      'id': existing.id,
      'rewardId': existing.rewardId,
      'tenantId': existing.businessId,
      'code': existing.code,
      'status': 3,
      'pointsCost': existing.pointsCost,
      'issuedAt': existing.issuedAt.toIso8601String(),
    });
    _pending[couponId] = cancelled;
    return cancelled;
  }

  @override
  Future<List<Coupon>> myCoupons() async => [
    Coupon.fromJson(const {
      'id': 'cpn-1',
      'rewardId': 'rew-1',
      'tenantId': 'tenant-1',
      'rewardNameEn': 'Free Large Latte',
      'code': 'CB-8F3K2Q',
      'status': 0,
      'issuedAt': '2026-08-04T10:00:00',
    }),
  ];

  @override
  Future<List<Campaign>> myCampaigns() async => [
    Campaign.fromJson(const {
      'id': 'cam-1',
      'tenantId': 'tenant-1',
      'businessName': 'Cedar & Bean Coffee',
      'nameEn': 'Double Points Weekend',
      'type': 1,
      'startDate': '2026-08-01T00:00:00',
      'endDate': '2026-12-09T00:00:00',
    }),
  ];

  @override
  Future<List<Campaign>> campaignsForBusiness(String tenantId) async =>
      tenantId == 'tenant-1' ? myCampaigns() : const <Campaign>[];

  @override
  Future<List<AppNotification>> notifications({int max = 50}) async =>
      List.of(_notifications);

  @override
  Future<int> unreadCount() async =>
      _notifications.where((n) => !n.read).length;

  @override
  Future<void> markNotificationRead(String id) async {
    for (var i = 0; i < _notifications.length; i++) {
      if (_notifications[i].id == id) {
        _notifications[i] = _notifications[i].copyWith(read: true);
      }
    }
  }

  @override
  Future<void> markAllNotificationsRead() async {
    for (var i = 0; i < _notifications.length; i++) {
      _notifications[i] = _notifications[i].copyWith(read: true);
    }
  }

  @override
  Future<ReferralProgram> referral(List<Business> joined) async =>
      ReferralProgram(
        codes: [
          for (final b in joined)
            ReferralCode(business: b, code: 'code-${b.id}'),
        ],
        history: const [],
      );

  @override
  Future<List<String>> myFollowedTenantIds() async => const [];

  @override
  Future<void> follow(String tenantId) async {}

  @override
  Future<void> unfollow(String tenantId) async {}
}
