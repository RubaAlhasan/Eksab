import 'package:flutter/material.dart';

import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';

/// Domain models for the customer app, mapped from the API's DTOs in
/// `src/Eksabli.Application.Contracts/`.
///
/// Where a field the prototype showed has no server source, it is nullable here
/// rather than invented — the UI hides it instead of displaying a fake value.

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

  /// From `CustomerProfileDto`; null until the customer supplies one.
  final DateTime? dateOfBirth;
  final String? gender;

  /// `CustomerProfileDto.creationTime` — when the account was created.
  final DateTime? memberSince;

  String get fullName => '$firstName $lastName'.trim();
}

/// Maps `CustomerBusinessDto` (`/api/app/customer-business`).
class Business {
  const Business({
    required this.id,
    required this.name,
    required this.category,
    required this.initials,
    required this.gradient,
    required this.branches,
    required this.businessProfileId,
    required this.hasLogo,
    this.description,
    this.website,
    this.distanceKm,
    this.following = false,
    this.member = false,
  });

  factory Business.fromJson(Map<String, dynamic> json) {
    final name = (json['name'] as String?)?.trim() ?? '';
    final tenantId = (json['tenantId'] as String?) ?? '';

    return Business(
      id: tenantId,
      name: name,
      category:
          (json['categoryNameEn'] as String?)?.trim() ??
          (json['categoryNameAr'] as String?)?.trim() ??
          '',
      initials: initialsFor(name),
      // No brand colour exists server-side, so derive one deterministically
      // from the tenant id: stable per business, and stable across sessions.
      gradient: gradientFor(tenantId),
      branches: (json['branchCount'] as num?)?.toInt() ?? 0,
      businessProfileId: (json['businessProfileId'] as String?) ?? '',
      hasLogo: json['hasLogo'] as bool? ?? false,
      description:
          (json['descriptionEn'] as String?)?.trim() ??
          (json['descriptionAr'] as String?)?.trim(),
      website: json['website'] as String?,
      distanceKm: (json['distanceKm'] as num?)?.toDouble(),
    );
  }

  final String id;
  final String name;
  final String category;
  final String initials;
  final BrandGradient gradient;
  final int branches;
  final String businessProfileId;
  final bool hasLogo;
  final String? description;
  final String? website;

  /// Null unless the directory was queried with coordinates.
  final double? distanceKm;

  /// Derived client-side by cross-referencing follows and memberships.
  final bool following;
  final bool member;

  Business copyWith({bool? following, bool? member}) => Business(
    id: id,
    name: name,
    category: category,
    initials: initials,
    gradient: gradient,
    branches: branches,
    businessProfileId: businessProfileId,
    hasLogo: hasLogo,
    description: description,
    website: website,
    distanceKm: distanceKm,
    following: following ?? this.following,
    member: member ?? this.member,
  );

  static String initialsFor(String name) {
    final words = name
        .split(RegExp(r'[\s&]+'))
        .where((w) => w.isNotEmpty && RegExp('[A-Za-z0-9]').hasMatch(w[0]))
        .toList();
    if (words.isEmpty) return '?';
    if (words.length == 1) {
      return words.first.substring(0, words.first.length >= 2 ? 2 : 1).toUpperCase();
    }
    return (words[0][0] + words[1][0]).toUpperCase();
  }

  /// Stable colour per id — same business always gets the same gradient.
  static BrandGradient gradientFor(String id) {
    if (id.isEmpty) return BrandGradient.values.first;
    var hash = 0;
    for (final unit in id.codeUnits) {
      hash = (hash * 31 + unit) & 0x7FFFFFFF;
    }
    return BrandGradient.values[hash % BrandGradient.values.length];
  }
}

enum MembershipStatus {
  active,
  frozen;

  static MembershipStatus fromJson(Object? raw) =>
      _enumFromJson(raw, MembershipStatus.values, MembershipStatus.active);

  String get label => this == MembershipStatus.active ? 'Active' : 'Frozen';
}

/// Combines `MembershipDto` and `PointsWalletDto` — the app always needs both
/// together (a membership is meaningless without its balance).
class Membership {
  const Membership({
    required this.businessId,
    required this.membershipId,
    required this.balance,
    required this.lifetimeEarned,
    required this.lifetimeRedeemed,
    required this.status,
    this.joinedAt,
    this.tier,
  });

  factory Membership.fromWalletJson(Map<String, dynamic> json) => Membership(
    businessId: (json['tenantId'] as String?) ?? '',
    membershipId: (json['membershipId'] as String?) ?? '',
    balance: (json['balance'] as num?)?.toInt() ?? 0,
    lifetimeEarned: (json['lifetimeEarned'] as num?)?.toInt() ?? 0,
    lifetimeRedeemed: (json['lifetimeRedeemed'] as num?)?.toInt() ?? 0,
    status: MembershipStatus.active,
    tier: (json['currentTierName'] as String?)?.trim(),
  );

  final String businessId;
  final String membershipId;
  final int balance;
  final int lifetimeEarned;
  final int lifetimeRedeemed;
  final MembershipStatus status;

  /// From `MembershipDto`; the wallet endpoint alone does not carry it.
  final DateTime? joinedAt;

  /// `PointsWalletDto.currentTierName`. Null when the business defines no tiers.
  final String? tier;

  Membership withJoinedAt(DateTime? value) => Membership(
    businessId: businessId,
    membershipId: membershipId,
    balance: balance,
    lifetimeEarned: lifetimeEarned,
    lifetimeRedeemed: lifetimeRedeemed,
    status: status,
    joinedAt: value,
    tier: tier,
  );
}

enum TransactionType {
  earn,
  redeem,
  expire,
  adjust,
  refund;

  static TransactionType fromJson(Object? raw) =>
      _enumFromJson(raw, TransactionType.values, TransactionType.earn);

  String get label => switch (this) {
    TransactionType.earn => 'Earn',
    TransactionType.redeem => 'Redeem',
    TransactionType.expire => 'Expire',
    TransactionType.adjust => 'Adjust',
    TransactionType.refund => 'Refund',
  };
}

enum TransactionSource {
  purchase,
  campaign,
  referral,
  birthday,
  manual,
  reward;

  static TransactionSource fromJson(Object? raw) =>
      _enumFromJson(raw, TransactionSource.values, TransactionSource.purchase);

  String get label => switch (this) {
    TransactionSource.purchase => 'Purchase',
    TransactionSource.campaign => 'Campaign',
    TransactionSource.referral => 'Referral',
    TransactionSource.birthday => 'Birthday bonus',
    TransactionSource.manual => 'Manual adjustment',
    TransactionSource.reward => 'Reward redemption',
  };
}

/// Maps `PointsTransactionDto`.
class PointTransaction {
  const PointTransaction({
    required this.id,
    required this.businessId,
    required this.type,
    required this.points,
    required this.source,
    required this.date,
    this.reason,
  });

  factory PointTransaction.fromJson(Map<String, dynamic> json, String businessId) =>
      PointTransaction(
        id: (json['id'] as String?) ?? '',
        businessId: businessId,
        type: TransactionType.fromJson(json['type']),
        points: (json['points'] as num?)?.toInt() ?? 0,
        source: TransactionSource.fromJson(json['source']),
        date: DateTime.tryParse('${json['creationTime']}') ?? DateTime.now(),
        reason: (json['reason'] as String?)?.trim(),
      );

  final String id;
  final String businessId;
  final TransactionType type;
  final int points;
  final TransactionSource source;
  final DateTime date;

  /// Free-text note from staff, when present.
  final String? reason;

  bool get isCredit => points > 0;

  /// The server has no description field, so build one from source + reason —
  /// which is what those fields are for.
  String get description {
    if (reason != null && reason!.isNotEmpty) return reason!;
    return switch (type) {
      TransactionType.expire => 'Points expired',
      TransactionType.refund => 'Refund — ${source.label}',
      _ => source.label,
    };
  }
}

enum RewardType {
  discount,
  freeProduct,
  giftCard;

  static RewardType fromJson(Object? raw) =>
      _enumFromJson(raw, RewardType.values, RewardType.discount);

  /// The prototype used a per-reward emoji and the server has no such field,
  /// so these are derived per type. Material icons rather than emoji: CanvasKit
  /// has no font fallback for several emoji (🏷️ and 🎟️ both render as tofu on
  /// web), and an icon renders identically on every platform.
  IconData get icon => switch (this) {
    RewardType.discount => Icons.sell_outlined,
    RewardType.freeProduct => Icons.card_giftcard_rounded,
    RewardType.giftCard => Icons.credit_card_rounded,
  };

  Color get tone => switch (this) {
    RewardType.discount => AppColors.danger500,
    RewardType.freeProduct => AppColors.success600,
    RewardType.giftCard => AppColors.info600,
  };
}

/// Maps `RewardDto`.
class Reward {
  const Reward({
    required this.id,
    required this.businessId,
    required this.name,
    required this.type,
    required this.pointsCost,
    this.stock,
    this.validTo,
  });

  factory Reward.fromJson(Map<String, dynamic> json) => Reward(
    id: (json['id'] as String?) ?? '',
    businessId: (json['tenantId'] as String?) ?? '',
    name:
        (json['nameEn'] as String?)?.trim().isNotEmpty == true
        ? (json['nameEn'] as String).trim()
        : ((json['nameAr'] as String?)?.trim() ?? 'Reward'),
    type: RewardType.fromJson(json['type']),
    pointsCost: (json['pointsCost'] as num?)?.toInt() ?? 0,
    stock: (json['stockRemaining'] as num?)?.toInt(),
    validTo: DateTime.tryParse('${json['validTo']}'),
  );

  final String id;
  final String businessId;
  final String name;
  final RewardType type;
  final int pointsCost;

  /// `null` means unlimited stock.
  final int? stock;
  final DateTime? validTo;

  IconData get icon => type.icon;
  Color get tone => type.tone;
}

enum CouponStatus {
  issued,
  redeemed,
  expired,
  cancelled;

  static CouponStatus fromJson(Object? raw) =>
      _enumFromJson(raw, CouponStatus.values, CouponStatus.issued);

  String get label => switch (this) {
    CouponStatus.issued => 'Active',
    CouponStatus.redeemed => 'Used',
    CouponStatus.expired => 'Expired',
    CouponStatus.cancelled => 'Cancelled',
  };
}

/// Maps `CouponDto`.
class Coupon {
  const Coupon({
    required this.id,
    required this.rewardId,
    required this.businessId,
    required this.code,
    required this.status,
    required this.issuedAt,
    this.rewardName,
    this.redeemedAt,
  });

  factory Coupon.fromJson(Map<String, dynamic> json) => Coupon(
    id: (json['id'] as String?) ?? '',
    rewardId: (json['rewardId'] as String?) ?? '',
    businessId: (json['tenantId'] as String?) ?? '',
    code: (json['code'] as String?) ?? '',
    status: CouponStatus.fromJson(json['status']),
    issuedAt: DateTime.tryParse('${json['issuedAt']}') ?? DateTime.now(),
    rewardName:
        (json['rewardNameEn'] as String?)?.trim() ??
        (json['rewardNameAr'] as String?)?.trim(),
    redeemedAt: DateTime.tryParse('${json['redeemedAt']}'),
  );

  final String id;
  final String rewardId;
  final String businessId;
  final String code;
  final CouponStatus status;
  final DateTime issuedAt;
  final String? rewardName;
  final DateTime? redeemedAt;
}

enum NotificationTone {
  info,
  success,
  warning,
  error;

  static NotificationTone fromJson(Object? raw) =>
      _enumFromJson(raw, NotificationTone.values, NotificationTone.info);
}

/// Maps `UserNotificationDto`.
///
/// Note: the DTO carries no tenant reference, so a notification cannot be
/// attributed to a business — the UI shows a tone-coloured icon rather than a
/// business logo.
class AppNotification {
  const AppNotification({
    required this.id,
    required this.tone,
    required this.title,
    required this.body,
    required this.sentAt,
    required this.read,
    this.category,
  });

  factory AppNotification.fromJson(Map<String, dynamic> json) => AppNotification(
    id: (json['id'] as String?) ?? '',
    tone: NotificationTone.fromJson(json['type']),
    title: (json['title'] as String?) ?? '',
    body: (json['message'] as String?) ?? '',
    sentAt: DateTime.tryParse('${json['creationTime']}') ?? DateTime.now(),
    read: json['isRead'] as bool? ?? false,
    category: (json['category'] as String?)?.trim(),
  );

  final String id;
  final NotificationTone tone;
  final String title;
  final String body;
  final DateTime sentAt;
  final bool read;
  final String? category;

  AppNotification copyWith({bool? read}) => AppNotification(
    id: id,
    tone: tone,
    title: title,
    body: body,
    sentAt: sentAt,
    read: read ?? this.read,
    category: category,
  );
}

enum ReferralStatus {
  pending,
  completed,
  rewarded;

  static ReferralStatus fromJson(Object? raw) =>
      _enumFromJson(raw, ReferralStatus.values, ReferralStatus.pending);

  String get label => switch (this) {
    ReferralStatus.pending => 'Pending',
    ReferralStatus.completed => 'Completed',
    ReferralStatus.rewarded => 'Rewarded',
  };
}

/// Maps `ReferralDto`. The referee's name is not exposed, so the UI shows the
/// status and date rather than inventing a person.
class ReferralInvite {
  const ReferralInvite({required this.id, required this.status, this.date});

  factory ReferralInvite.fromJson(Map<String, dynamic> json) => ReferralInvite(
    id: (json['id'] as String?) ?? '',
    status: ReferralStatus.fromJson(json['status']),
    date: DateTime.tryParse('${json['creationTime']}'),
  );

  final String id;
  final ReferralStatus status;
  final DateTime? date;
}

/// One share code, scoped to a business.
///
/// `GET /api/app/referral/my-code` requires a `tenantId` and returns the
/// customer's **membership id** for that business — so a code exists per
/// business joined, not one per customer as the prototype assumed.
class ReferralCode {
  const ReferralCode({required this.business, required this.code});

  final Business business;
  final String code;

  String get link => 'https://eksabli.app/r/$code';
}

/// The referral picture: a code per joined business, plus the invite history
/// (which is customer-wide). Counts are derived rather than served.
class ReferralProgram {
  const ReferralProgram({required this.codes, required this.history});

  final List<ReferralCode> codes;
  final List<ReferralInvite> history;

  int get invited => history.length;
  int get joined =>
      history.where((h) => h.status != ReferralStatus.pending).length;
  int get rewarded =>
      history.where((h) => h.status == ReferralStatus.rewarded).length;
}

enum CampaignType {
  birthday,
  doublePoints,
  spendXGetY,
  winBack,
  vip,
  newCustomer,
  referral;

  static CampaignType fromJson(Object? raw) =>
      _enumFromJson(raw, CampaignType.values, CampaignType.doublePoints);

  String get label => switch (this) {
    CampaignType.birthday => 'Birthday',
    CampaignType.doublePoints => 'Double Points',
    CampaignType.spendXGetY => 'Spend & Get',
    CampaignType.winBack => 'Win Back',
    CampaignType.vip => 'VIP',
    CampaignType.newCustomer => 'New Customer',
    CampaignType.referral => 'Referral',
  };
}

/// Maps `CustomerCampaignDto` (`/api/app/customer-campaign`).
///
/// The server only returns campaigns that are Active, inside their date window,
/// from an Approved business, and whose target segment includes this customer —
/// so anything here is genuinely claimable.
class Campaign {
  const Campaign({
    required this.id,
    required this.businessId,
    required this.businessName,
    required this.name,
    required this.type,
    required this.startDate,
    required this.endDate,
  });

  factory Campaign.fromJson(Map<String, dynamic> json) {
    final en = (json['nameEn'] as String?)?.trim() ?? '';
    final ar = (json['nameAr'] as String?)?.trim() ?? '';
    return Campaign(
      id: (json['id'] as String?) ?? '',
      businessId: (json['tenantId'] as String?) ?? '',
      businessName: (json['businessName'] as String?)?.trim() ?? '',
      name: en.isNotEmpty ? en : ar,
      type: CampaignType.fromJson(json['type']),
      startDate: DateTime.tryParse('${json['startDate']}') ?? DateTime.now(),
      endDate: DateTime.tryParse('${json['endDate']}') ?? DateTime.now(),
    );
  }

  final String id;
  final String businessId;
  final String businessName;
  final String name;
  final CampaignType type;
  final DateTime startDate;
  final DateTime endDate;
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

/// Shared enum decoder: the API serialises enums as ints, but tolerate a name
/// string too in case a controller ever switches to `JsonStringEnumConverter`.
T _enumFromJson<T extends Enum>(Object? raw, List<T> values, T fallback) {
  if (raw is int && raw >= 0 && raw < values.length) return values[raw];
  if (raw is String) {
    final asInt = int.tryParse(raw);
    if (asInt != null && asInt >= 0 && asInt < values.length) return values[asInt];
    for (final v in values) {
      if (v.name.toLowerCase() == raw.toLowerCase()) return v;
    }
  }
  return fallback;
}
