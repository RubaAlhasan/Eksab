import 'package:dio/dio.dart';

import '../../shared/models/models.dart';
import '../auth/auth_exception.dart';

/// Thin repositories over the customer-facing controllers in
/// `src/Eksabli.HttpApi/Controllers/`.
///
/// Each takes the authenticated [Dio] client, so token attachment and refresh
/// are handled by its interceptor rather than repeated here. Errors are mapped
/// to [AuthException] so screens have one failure type to render.
class EksabliApi {
  const EksabliApi(this._client);

  final Dio _client;

  // -------------------------------------------------------------------------
  // Businesses — /api/app/customer-business
  // -------------------------------------------------------------------------

  /// Directory search. Pass coordinates to get `distanceKm` and nearest-first
  /// ordering; without them results come back alphabetically.
  Future<List<Business>> searchBusinesses({
    String? query,
    String? categoryId,
    double? latitude,
    double? longitude,
    int skip = 0,
    int max = 50,
  }) => _guard(() async {
    final response = await _client.get<Map<String, dynamic>>(
      '/api/app/customer-business',
      queryParameters: {
        if (query != null && query.isNotEmpty) 'filterText': query,
        if (categoryId != null) 'categoryId': categoryId,
        if (latitude != null) 'latitude': latitude,
        if (longitude != null) 'longitude': longitude,
        'skipCount': skip,
        'maxResultCount': max,
      },
    );
    return _items(response.data).map(Business.fromJson).toList();
  });

  Future<Business> getBusiness(String tenantId) => _guard(() async {
    final response = await _client.get<Map<String, dynamic>>(
      '/api/app/customer-business/$tenantId',
    );
    return Business.fromJson(response.data ?? const {});
  });

  /// Batch tenant-id resolution — wallet, coupon and membership responses carry
  /// bare ids, so this turns a page of them into names in one request.
  Future<Map<String, Business>> lookupBusinesses(List<String> tenantIds) =>
      _guard(() async {
        final ids = tenantIds.where((id) => id.isNotEmpty).toSet().toList();
        if (ids.isEmpty) return <String, Business>{};

        final response = await _client.post<List<dynamic>>(
          '/api/app/customer-business/lookup',
          data: {'tenantIds': ids},
        );
        final businesses = (response.data ?? const [])
            .whereType<Map<String, dynamic>>()
            .map(Business.fromJson);
        return {for (final b in businesses) b.id: b};
      });

  // -------------------------------------------------------------------------
  // Memberships & wallet
  // -------------------------------------------------------------------------

  /// `GET /api/app/memberships/my/wallets` plus `GET /api/app/memberships/my`
  /// merged, so each membership carries both its balance and its join date.
  Future<List<Membership>> myMemberships() => _guard(() async {
    final wallets = await _client.get<List<dynamic>>(
      '/api/app/memberships/my/wallets',
    );
    final memberships = await _client.get<List<dynamic>>(
      '/api/app/memberships/my',
    );

    final joinedAt = <String, DateTime?>{
      for (final m in (memberships.data ?? const []).whereType<Map<String, dynamic>>())
        (m['tenantId'] as String? ?? ''): DateTime.tryParse('${m['joinedAt']}'),
    };

    return (wallets.data ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(Membership.fromWalletJson)
        .map((m) => m.withJoinedAt(joinedAt[m.businessId]))
        .toList();
  });

  Future<void> joinBusiness(String tenantId) => _guard(() async {
    await _client.post<dynamic>(
      '/api/app/memberships/join',
      data: {'tenantId': tenantId},
    );
  });

  /// Short-lived token behind the wallet QR. Server-issued deliberately — an
  /// offline or client-minted code must never be treated as valid.
  Future<String> walletQrToken() => _guard(() async {
    final response = await _client.post<Map<String, dynamic>>(
      '/api/app/memberships/my/wallet-qr-token',
    );
    return (response.data?['token'] as String?) ??
        (response.data?['value'] as String?) ??
        '';
  });

  Future<List<PointTransaction>> transactions(
    String tenantId, {
    int skip = 0,
    int max = 100,
  }) => _guard(() async {
    final response = await _client.get<Map<String, dynamic>>(
      '/api/app/wallet/$tenantId/transactions',
      queryParameters: {'skipCount': skip, 'maxResultCount': max},
    );
    return _items(response.data)
        .map((j) => PointTransaction.fromJson(j, tenantId))
        .toList();
  });

  // -------------------------------------------------------------------------
  // Rewards & coupons
  // -------------------------------------------------------------------------

  Future<List<Reward>> rewardCatalog(String tenantId) => _guard(() async {
    final response = await _client.get<Map<String, dynamic>>(
      '/api/app/coupon/catalog/$tenantId',
      queryParameters: const {'maxResultCount': 100},
    );
    return _items(response.data).map(Reward.fromJson).toList();
  });

  /// `RedeemRewardDto` requires **both** ids — the service switches tenant
  /// context by `tenantId` before looking up the membership, so omitting it
  /// fails with "You haven't joined this business yet" rather than a
  /// validation error.
  Future<Coupon> redeemReward({
    required String tenantId,
    required String rewardId,
  }) => _guard(() async {
    final response = await _client.post<Map<String, dynamic>>(
      '/api/app/coupon/redeem',
      data: {'tenantId': tenantId, 'rewardId': rewardId},
    );
    return Coupon.fromJson(response.data ?? const {});
  });

  Future<List<Coupon>> myCoupons() => _guard(() async {
    final response = await _client.get<List<dynamic>>('/api/app/coupon/my');
    return (response.data ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(Coupon.fromJson)
        .toList();
  });

  // -------------------------------------------------------------------------
  // Campaigns — /api/app/customer-campaign
  // -------------------------------------------------------------------------

  /// Live campaigns across every business the customer has joined. The server
  /// applies segment targeting, so anything returned actually applies to them.
  Future<List<Campaign>> myCampaigns() => _guard(() async {
    final response = await _client.get<List<dynamic>>(
      '/api/app/customer-campaign/my',
    );
    return (response.data ?? const [])
        .whereType<Map<String, dynamic>>()
        .map(Campaign.fromJson)
        .toList();
  });

  Future<List<Campaign>> campaignsForBusiness(String tenantId) =>
      _guard(() async {
        final response = await _client.get<List<dynamic>>(
          '/api/app/customer-campaign/business/$tenantId',
        );
        return (response.data ?? const [])
            .whereType<Map<String, dynamic>>()
            .map(Campaign.fromJson)
            .toList();
      });

  // -------------------------------------------------------------------------
  // Notifications
  // -------------------------------------------------------------------------

  Future<List<AppNotification>> notifications({int max = 50}) =>
      _guard(() async {
        final response = await _client.get<Map<String, dynamic>>(
          '/api/app/user-notifications',
          queryParameters: {'maxResultCount': max},
        );
        return _items(response.data).map(AppNotification.fromJson).toList();
      });

  Future<int> unreadCount() => _guard(() async {
    final response = await _client.get<dynamic>(
      '/api/app/user-notifications/unread-count',
    );
    final data = response.data;
    return data is int ? data : int.tryParse('$data') ?? 0;
  });

  Future<void> markNotificationRead(String id) => _guard(() async {
    await _client.post<dynamic>('/api/app/user-notifications/$id/mark-as-read');
  });

  Future<void> markAllNotificationsRead() => _guard(() async {
    await _client.post<dynamic>('/api/app/user-notifications/mark-all-as-read');
  });

  // -------------------------------------------------------------------------
  // Referrals & follows
  // -------------------------------------------------------------------------

  /// The referral code is per-business: `my-code` requires a `tenantId` and
  /// returns that membership's id. So this fetches one code per joined
  /// business and pairs it with the customer-wide invite history.
  Future<ReferralProgram> referral(List<Business> joined) => _guard(() async {
    final codes = <ReferralCode>[];
    for (final business in joined) {
      try {
        final response = await _client.get<Map<String, dynamic>>(
          '/api/app/referral/my-code',
          queryParameters: {'tenantId': business.id},
        );
        final code = (response.data?['code'] as String?) ?? '';
        if (code.isNotEmpty) {
          codes.add(ReferralCode(business: business, code: code));
        }
      } on DioException {
        // A business can refuse (e.g. membership frozen) — skip that one
        // rather than failing the whole screen.
        continue;
      }
    }

    final mine = await _client.get<List<dynamic>>('/api/app/referral/my');

    return ReferralProgram(
      codes: codes,
      history: (mine.data ?? const [])
          .whereType<Map<String, dynamic>>()
          .map(ReferralInvite.fromJson)
          .toList(),
    );
  });

  Future<List<String>> myFollowedTenantIds() => _guard(() async {
    final response = await _client.get<List<dynamic>>('/api/app/follow/my');
    return (response.data ?? const [])
        .whereType<Map<String, dynamic>>()
        .map((j) => (j['tenantId'] as String?) ?? '')
        .where((id) => id.isNotEmpty)
        .toList();
  });

  Future<void> follow(String tenantId) => _guard(() async {
    await _client.post<dynamic>('/api/app/follow/$tenantId');
  });

  Future<void> unfollow(String tenantId) => _guard(() async {
    await _client.delete<dynamic>('/api/app/follow/$tenantId');
  });

  // -------------------------------------------------------------------------

  /// ABP returns paged results as `{ totalCount, items: [...] }`; some
  /// endpoints return a bare list. Accept either.
  static List<Map<String, dynamic>> _items(Object? data) {
    if (data is List) return data.whereType<Map<String, dynamic>>().toList();
    if (data is Map<String, dynamic>) {
      final items = data['items'];
      if (items is List) return items.whereType<Map<String, dynamic>>().toList();
    }
    return const [];
  }

  static Future<T> _guard<T>(Future<T> Function() call) async {
    try {
      return await call();
    } on DioException catch (error) {
      throw AuthException.fromDio(error);
    }
  }
}
