import '../../app/theme/app_tokens.dart';

/// Domain models for the customer app. Field names and value sets mirror the
/// prototype's demo data (`prototype/assets/js/demo-data.js`), which in turn
/// mirrors the backend contracts described in
/// `docs/eksabli-loyalty-platform/03-database-design.md`.

class Customer {
  const Customer({
    required this.id,
    required this.firstName,
    required this.lastName,
    required this.initials,
    required this.email,
    required this.phone,
    this.dateOfBirth,
    this.gender,
    this.memberSince,
  });

  /// Placeholder identity used only while a session is being restored, so the
  /// UI never has to null-check the signed-in user. Deliberately blank rather
  /// than fixture data — showing someone else's name would be worse than
  /// showing nothing.
  static const empty = Customer(
    id: '',
    firstName: '',
    lastName: '',
    initials: '?',
    email: '',
    phone: '',
  );

  final String id;
  final String firstName;
  final String lastName;
  final String initials;
  final String email;
  final String phone;

  /// Null when the server has not supplied it. ABP's `/api/account/my-profile`
  /// carries no date of birth or gender, and Eksabli's `CustomerProfileDto`
  /// (which does) is not exposed over HTTP yet.
  final DateTime? dateOfBirth;
  final String? gender;

  /// Null for the same reason — ABP's profile endpoint has no creation date.
  final DateTime? memberSince;

  String get fullName => '$firstName $lastName'.trim();

  Customer copyWith({
    String? firstName,
    String? lastName,
    String? email,
    String? phone,
    DateTime? dateOfBirth,
    String? gender,
  }) {
    final first = firstName ?? this.firstName;
    final last = lastName ?? this.lastName;
    final initials = _initial(first) + _initial(last);
    return Customer(
      id: id,
      firstName: first,
      lastName: last,
      initials: initials.isEmpty ? '?' : initials,
      email: email ?? this.email,
      phone: phone ?? this.phone,
      dateOfBirth: dateOfBirth ?? this.dateOfBirth,
      gender: gender ?? this.gender,
      memberSince: memberSince,
    );
  }

  static String _initial(String value) =>
      value.isEmpty ? '' : value[0].toUpperCase();
}

class Business {
  const Business({
    required this.id,
    required this.name,
    required this.category,
    required this.initials,
    required this.gradient,
    required this.rating,
    required this.branches,
    required this.distanceKm,
    required this.following,
    required this.member,
  });

  final String id;
  final String name;
  final String category;
  final String initials;
  final BrandGradient gradient;
  final double rating;
  final int branches;
  final double distanceKm;
  final bool following;
  final bool member;

  Business copyWith({bool? following, bool? member}) => Business(
    id: id,
    name: name,
    category: category,
    initials: initials,
    gradient: gradient,
    rating: rating,
    branches: branches,
    distanceKm: distanceKm,
    following: following ?? this.following,
    member: member ?? this.member,
  );
}

class Membership {
  const Membership({
    required this.businessId,
    required this.membershipId,
    required this.joinedAt,
    required this.status,
    required this.balance,
    required this.lifetimeEarned,
    required this.lifetimeRedeemed,
    required this.tier,
    required this.tierProgressPct,
    required this.nextTier,
    required this.pointsToNextTier,
  });

  final String businessId;
  final String membershipId;
  final DateTime joinedAt;
  final String status;
  final int balance;
  final int lifetimeEarned;
  final int lifetimeRedeemed;
  final String tier;
  final int tierProgressPct;
  final String nextTier;
  final int pointsToNextTier;
}

enum TransactionType { earn, redeem, adjust, expire, refund }

class PointTransaction {
  const PointTransaction({
    required this.id,
    required this.businessId,
    required this.type,
    required this.points,
    required this.source,
    required this.description,
    required this.date,
  });

  final String id;
  final String businessId;
  final TransactionType type;
  final int points;
  final String source;
  final String description;
  final DateTime date;

  bool get isCredit => points > 0;
}

enum RewardType { freeProduct, discount, giftCard }

class Reward {
  const Reward({
    required this.id,
    required this.businessId,
    required this.name,
    required this.type,
    required this.pointsCost,
    required this.stock,
    required this.emoji,
    required this.validTo,
  });

  final String id;
  final String businessId;
  final String name;
  final RewardType type;
  final int pointsCost;

  /// `null` means unlimited stock.
  final int? stock;
  final String emoji;
  final DateTime validTo;
}

enum CouponStatus { issued, redeemed, expired, cancelled }

class Coupon {
  const Coupon({
    required this.id,
    required this.rewardId,
    required this.businessId,
    required this.code,
    required this.status,
    required this.issuedAt,
    this.redeemedAt,
    this.branch,
  });

  final String id;
  final String rewardId;
  final String businessId;
  final String code;
  final CouponStatus status;
  final DateTime issuedAt;
  final DateTime? redeemedAt;
  final String? branch;
}

enum CampaignType { doublePoints, discount, birthday, winBack, newCustomer, referral }

enum CampaignStatus { active, draft, ended }

class Campaign {
  const Campaign({
    required this.id,
    required this.businessId,
    required this.name,
    required this.type,
    required this.status,
    required this.startDate,
    required this.endDate,
    required this.description,
  });

  final String id;
  final String businessId;
  final String name;
  final CampaignType type;
  final CampaignStatus status;
  final DateTime startDate;
  final DateTime endDate;
  final String description;
}

enum NotificationChannel { push, email, sms, inApp }

class AppNotification {
  const AppNotification({
    required this.id,
    required this.businessId,
    required this.channel,
    required this.title,
    required this.body,
    required this.sentAt,
    required this.read,
  });

  final String id;
  final String businessId;
  final NotificationChannel channel;
  final String title;
  final String body;
  final DateTime sentAt;
  final bool read;

  AppNotification copyWith({bool? read}) => AppNotification(
    id: id,
    businessId: businessId,
    channel: channel,
    title: title,
    body: body,
    sentAt: sentAt,
    read: read ?? this.read,
  );
}

class ReferralInvite {
  const ReferralInvite({
    required this.name,
    required this.status,
    required this.date,
  });

  final String name;
  final String status; // Rewarded | Completed | Pending
  final DateTime date;

  String get initials =>
      name.split(' ').where((w) => w.isNotEmpty).map((w) => w[0]).join();
}

class ReferralProgram {
  const ReferralProgram({
    required this.code,
    required this.link,
    required this.invited,
    required this.joined,
    required this.pointsEarned,
    required this.history,
  });

  final String code;
  final String link;
  final int invited;
  final int joined;
  final int pointsEarned;
  final List<ReferralInvite> history;
}

class LinkedDevice {
  const LinkedDevice({
    required this.name,
    required this.location,
    required this.isCurrent,
  });

  final String name;
  final String location;
  final bool isCurrent;
}
