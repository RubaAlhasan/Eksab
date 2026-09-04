import '../../app/theme/app_tokens.dart';
import '../../shared/models/models.dart';

/// In-memory fixture data, ported from `prototype/assets/js/demo-data.js`.
///
/// This stands in for the API layer until the ABP backend endpoints are wired
/// up. Everything the UI needs goes through [DemoData], so swapping in a real
/// repository later is a one-file change per feature rather than a
/// screen-by-screen rewrite. All names and figures are fictional.
abstract final class DemoData {
  static final Customer currentCustomer = Customer(
    id: 'cus_001',
    firstName: 'Layla',
    lastName: 'Haddad',
    initials: 'LH',
    email: 'layla.haddad@example.com',
    phone: '+971 50 123 4567',
    dateOfBirth: DateTime(1996, 3, 14),
    gender: 'Female',
    memberSince: DateTime(2024, 11, 2),
  );

  static const List<Business> businesses = [
    Business(
      id: 'biz_1',
      name: 'Cedar & Bean Coffee',
      category: 'Coffee Shop',
      initials: 'CB',
      gradient: BrandGradient.amber,
      rating: 4.8,
      branches: 6,
      distanceKm: 0.4,
      following: false,
      member: true,
    ),
    Business(
      id: 'biz_2',
      name: 'Pulse Fitness Club',
      category: 'Fitness & Gym',
      initials: 'PF',
      gradient: BrandGradient.rose,
      rating: 4.6,
      branches: 3,
      distanceKm: 1.2,
      following: false,
      member: true,
    ),
    Business(
      id: 'biz_3',
      name: 'Bloom Beauty Bar',
      category: 'Beauty & Spa',
      initials: 'BB',
      gradient: BrandGradient.pinkFuchsia,
      rating: 4.9,
      branches: 2,
      distanceKm: 2.1,
      following: true,
      member: false,
    ),
    Business(
      id: 'biz_4',
      name: 'Cornerstone Bookshop',
      category: 'Books & Stationery',
      initials: 'CS',
      gradient: BrandGradient.emerald,
      rating: 4.7,
      branches: 4,
      distanceKm: 0.9,
      following: false,
      member: true,
    ),
    Business(
      id: 'biz_5',
      name: 'Skyline Electronics',
      category: 'Electronics',
      initials: 'SE',
      gradient: BrandGradient.sky,
      rating: 4.4,
      branches: 8,
      distanceKm: 3.5,
      following: true,
      member: false,
    ),
    Business(
      id: 'biz_6',
      name: 'Olive Grove Restaurant',
      category: 'Restaurant',
      initials: 'OG',
      gradient: BrandGradient.limeGreen,
      rating: 4.5,
      branches: 5,
      distanceKm: 1.7,
      following: false,
      member: false,
    ),
    Business(
      id: 'biz_7',
      name: 'Petal & Stem Florist',
      category: 'Florist',
      initials: 'PS',
      gradient: BrandGradient.violetPurple,
      rating: 4.9,
      branches: 1,
      distanceKm: 0.6,
      following: false,
      member: false,
    ),
    Business(
      id: 'biz_8',
      name: 'Verve Sneaker Co.',
      category: 'Retail & Fashion',
      initials: 'VS',
      gradient: BrandGradient.indigoPrimary,
      rating: 4.3,
      branches: 5,
      distanceKm: 2.8,
      following: false,
      member: false,
    ),
  ];

  static final List<Membership> memberships = [
    Membership(
      businessId: 'biz_1',
      membershipId: 'mem_1',
      joinedAt: DateTime(2024, 11, 5),
      status: 'Active',
      balance: 1250,
      lifetimeEarned: 3400,
      lifetimeRedeemed: 2150,
      tier: 'Gold',
      tierProgressPct: 68,
      nextTier: 'Platinum',
      pointsToNextTier: 750,
    ),
    Membership(
      businessId: 'biz_2',
      membershipId: 'mem_2',
      joinedAt: DateTime(2025, 1, 18),
      status: 'Active',
      balance: 420,
      lifetimeEarned: 920,
      lifetimeRedeemed: 500,
      tier: 'Silver',
      tierProgressPct: 40,
      nextTier: 'Gold',
      pointsToNextTier: 580,
    ),
    Membership(
      businessId: 'biz_4',
      membershipId: 'mem_3',
      joinedAt: DateTime(2025, 3, 22),
      status: 'Active',
      balance: 95,
      lifetimeEarned: 95,
      lifetimeRedeemed: 0,
      tier: 'Bronze',
      tierProgressPct: 19,
      nextTier: 'Silver',
      pointsToNextTier: 405,
    ),
  ];

  static final List<PointTransaction> transactions = [
    PointTransaction(
      id: 'txn_1',
      businessId: 'biz_1',
      type: TransactionType.earn,
      points: 45,
      source: 'Purchase',
      description: 'In-store purchase — Branch: Downtown',
      date: DateTime(2026, 8, 4, 9, 15),
    ),
    PointTransaction(
      id: 'txn_2',
      businessId: 'biz_1',
      type: TransactionType.earn,
      points: 20,
      source: 'Campaign',
      description: 'Double Points Weekend',
      date: DateTime(2026, 8, 2, 13, 40),
    ),
    PointTransaction(
      id: 'txn_3',
      businessId: 'biz_1',
      type: TransactionType.redeem,
      points: -500,
      source: 'Reward',
      description: 'Redeemed: Free Large Latte',
      date: DateTime(2026, 7, 29, 8, 2),
    ),
    PointTransaction(
      id: 'txn_4',
      businessId: 'biz_2',
      type: TransactionType.earn,
      points: 100,
      source: 'Purchase',
      description: 'Monthly membership visit',
      date: DateTime(2026, 7, 28, 18, 20),
    ),
    PointTransaction(
      id: 'txn_5',
      businessId: 'biz_1',
      type: TransactionType.adjust,
      points: 15,
      source: 'Manual',
      description: 'Goodwill adjustment — Support Agent',
      date: DateTime(2026, 7, 25, 11),
    ),
    PointTransaction(
      id: 'txn_6',
      businessId: 'biz_4',
      type: TransactionType.earn,
      points: 95,
      source: 'Purchase',
      description: 'Book purchase — Branch: Main Street',
      date: DateTime(2026, 7, 20, 16, 45),
    ),
    PointTransaction(
      id: 'txn_7',
      businessId: 'biz_2',
      type: TransactionType.earn,
      points: 50,
      source: 'Referral',
      description: 'Referral bonus — friend joined',
      date: DateTime(2026, 7, 15, 10, 10),
    ),
    PointTransaction(
      id: 'txn_8',
      businessId: 'biz_1',
      type: TransactionType.expire,
      points: -30,
      source: 'Expiration',
      description: 'Points expired (12-month policy)',
      date: DateTime(2026, 7, 10),
    ),
    PointTransaction(
      id: 'txn_9',
      businessId: 'biz_2',
      type: TransactionType.earn,
      points: 100,
      source: 'Birthday',
      description: 'Birthday bonus points',
      date: DateTime(2026, 6, 28, 9),
    ),
    PointTransaction(
      id: 'txn_10',
      businessId: 'biz_1',
      type: TransactionType.earn,
      points: 60,
      source: 'Purchase',
      description: 'In-store purchase — Branch: Marina',
      date: DateTime(2026, 6, 22, 17, 30),
    ),
  ];

  static final List<Reward> rewards = [
    Reward(
      id: 'rew_1',
      businessId: 'biz_1',
      name: 'Free Large Latte',
      type: RewardType.freeProduct,
      pointsCost: 500,
      stock: 42,
      emoji: '☕',
      validTo: DateTime(2026, 12, 31),
    ),
    Reward(
      id: 'rew_2',
      businessId: 'biz_1',
      name: '20% Off Any Order',
      type: RewardType.discount,
      pointsCost: 350,
      stock: null,
      emoji: '🏷️',
      validTo: DateTime(2026, 12, 31),
    ),
    Reward(
      id: 'rew_3',
      businessId: 'biz_1',
      name: r'$25 Gift Card',
      type: RewardType.giftCard,
      pointsCost: 2000,
      stock: 8,
      emoji: '🎁',
      validTo: DateTime(2026, 10, 31),
    ),
    Reward(
      id: 'rew_4',
      businessId: 'biz_2',
      name: 'Free Personal Training Session',
      type: RewardType.freeProduct,
      pointsCost: 800,
      stock: 15,
      emoji: '🏋️',
      validTo: DateTime(2026, 11, 30),
    ),
    Reward(
      id: 'rew_5',
      businessId: 'biz_2',
      name: '1 Month Membership Extension',
      type: RewardType.freeProduct,
      pointsCost: 1500,
      stock: 5,
      emoji: '📅',
      validTo: DateTime(2026, 12, 31),
    ),
    Reward(
      id: 'rew_6',
      businessId: 'biz_4',
      name: r'$10 Off Next Purchase',
      type: RewardType.discount,
      pointsCost: 150,
      stock: null,
      emoji: '📚',
      validTo: DateTime(2026, 12, 31),
    ),
    Reward(
      id: 'rew_7',
      businessId: 'biz_4',
      name: 'Free Bookmark & Tote Bag',
      type: RewardType.freeProduct,
      pointsCost: 90,
      stock: 120,
      emoji: '👜',
      validTo: DateTime(2026, 12, 31),
    ),
  ];

  static final List<Coupon> coupons = [
    Coupon(
      id: 'cpn_1',
      rewardId: 'rew_1',
      businessId: 'biz_1',
      code: 'CB-8F3K2Q',
      status: CouponStatus.redeemed,
      issuedAt: DateTime(2026, 7, 29, 7, 58),
      redeemedAt: DateTime(2026, 7, 29, 8, 2),
      branch: 'Downtown',
    ),
    Coupon(
      id: 'cpn_2',
      rewardId: 'rew_2',
      businessId: 'biz_1',
      code: 'CB-9L2M7R',
      status: CouponStatus.issued,
      issuedAt: DateTime(2026, 8, 4, 10),
    ),
    Coupon(
      id: 'cpn_3',
      rewardId: 'rew_6',
      businessId: 'biz_4',
      code: 'CS-4T7Y1X',
      status: CouponStatus.expired,
      issuedAt: DateTime(2026, 5, 1, 9),
    ),
  ];

  static final List<Campaign> campaigns = [
    Campaign(
      id: 'cam_1',
      businessId: 'biz_1',
      name: 'Double Points Weekend',
      type: CampaignType.doublePoints,
      status: CampaignStatus.active,
      startDate: DateTime(2026, 8, 1),
      endDate: DateTime(2026, 8, 9),
      description: 'Earn 2x points on every purchase this weekend.',
    ),
    Campaign(
      id: 'cam_2',
      businessId: 'biz_8',
      name: '20% Off Sneakers',
      type: CampaignType.discount,
      status: CampaignStatus.active,
      startDate: DateTime(2026, 8, 1),
      endDate: DateTime(2026, 8, 15),
      description: 'Flat 20% off all sneaker styles.',
    ),
    Campaign(
      id: 'cam_3',
      businessId: 'biz_1',
      name: 'Birthday Bonus',
      type: CampaignType.birthday,
      status: CampaignStatus.active,
      startDate: DateTime(2026, 1, 1),
      endDate: DateTime(2026, 12, 31),
      description: '100 bonus points during your birthday month.',
    ),
    Campaign(
      id: 'cam_4',
      businessId: 'biz_2',
      name: 'Win-Back: We Miss You',
      type: CampaignType.winBack,
      status: CampaignStatus.draft,
      startDate: DateTime(2026, 8, 10),
      endDate: DateTime(2026, 8, 24),
      description: 'Targeting members inactive 60+ days.',
    ),
    Campaign(
      id: 'cam_5',
      businessId: 'biz_4',
      name: 'New Member Welcome',
      type: CampaignType.newCustomer,
      status: CampaignStatus.active,
      startDate: DateTime(2026, 1, 1),
      endDate: DateTime(2026, 12, 31),
      description: 'Welcome bonus for members joined within 7 days.',
    ),
    Campaign(
      id: 'cam_6',
      businessId: 'biz_1',
      name: 'Refer a Friend',
      type: CampaignType.referral,
      status: CampaignStatus.ended,
      startDate: DateTime(2026, 5, 1),
      endDate: DateTime(2026, 6, 30),
      description: 'Bonus points for referrer and referee.',
    ),
  ];

  static final List<AppNotification> notifications = [
    AppNotification(
      id: 'not_1',
      businessId: 'biz_1',
      channel: NotificationChannel.push,
      title: 'Double Points Weekend is live!',
      body: 'Earn 2x points on every order through Sunday.',
      sentAt: DateTime(2026, 8, 4, 8),
      read: false,
    ),
    AppNotification(
      id: 'not_2',
      businessId: 'biz_2',
      channel: NotificationChannel.push,
      title: 'Your Gold tier is close',
      body: 'Just 580 points to reach Gold at Pulse Fitness Club.',
      sentAt: DateTime(2026, 8, 3, 15, 20),
      read: false,
    ),
    AppNotification(
      id: 'not_3',
      businessId: 'biz_1',
      channel: NotificationChannel.inApp,
      title: 'Coupon redeemed',
      body: 'You redeemed Free Large Latte at Downtown branch.',
      sentAt: DateTime(2026, 7, 29, 8, 2),
      read: true,
    ),
    AppNotification(
      id: 'not_4',
      businessId: 'biz_4',
      channel: NotificationChannel.email,
      title: 'Welcome to Cornerstone Bookshop',
      body: 'Thanks for joining — here is 90 bonus points to start.',
      sentAt: DateTime(2026, 7, 22, 9),
      read: true,
    ),
    AppNotification(
      id: 'not_5',
      businessId: 'biz_8',
      channel: NotificationChannel.push,
      title: 'Verve Sneaker Co. — 20% off',
      body: 'A new offer just dropped for followers.',
      sentAt: DateTime(2026, 8, 2, 12),
      read: true,
    ),
  ];

  static final ReferralProgram referral = ReferralProgram(
    code: 'LAYLA25',
    link: 'https://eksabli.app/r/LAYLA25',
    invited: 8,
    joined: 5,
    pointsEarned: 250,
    history: [
      ReferralInvite(
        name: 'Omar S.',
        status: 'Rewarded',
        date: DateTime(2026, 7, 15),
      ),
      ReferralInvite(
        name: 'Nadia K.',
        status: 'Rewarded',
        date: DateTime(2026, 6, 30),
      ),
      ReferralInvite(
        name: 'Yousef A.',
        status: 'Pending',
        date: DateTime(2026, 8, 1),
      ),
      ReferralInvite(
        name: 'Rana M.',
        status: 'Completed',
        date: DateTime(2026, 7, 28),
      ),
    ],
  );

  static const List<LinkedDevice> devices = [
    LinkedDevice(name: 'iPhone 15 Pro', location: 'Dubai, UAE', isCurrent: true),
    LinkedDevice(
      name: 'Chrome — Windows',
      location: 'Dubai, UAE',
      isCurrent: false,
    ),
  ];

  /// Branch names the prototype cycles through when rendering a business's
  /// branch list; the real app will read these from the Branches endpoint.
  static const List<String> branchNames = [
    'Downtown',
    'Marina',
    'JBR',
    'Business Bay',
    'Al Ain',
    'Sharjah City Centre',
    'Deira',
    'Al Barsha',
  ];

  static const List<({String question, String answer})> faqs = [
    (
      question: 'How do I earn points?',
      answer:
          'Points are awarded automatically by staff at checkout — either by '
          'scanning your wallet QR or looking up your phone number.',
    ),
    (
      question: 'Do points expire?',
      answer:
          'Each business sets its own expiration policy, shown on that '
          "business's Points page. Expired points appear as a separate line in "
          'your transaction history.',
    ),
    (
      question: 'Can I transfer points to a friend?',
      answer:
          'Not yet — point transfer between customers is not available in this '
          'version.',
    ),
    (
      question: 'What happens if a business closes?',
      answer:
          'Your membership and balance are frozen, not deleted, so your history '
          'at other businesses is unaffected.',
    ),
    (
      question: 'How do I delete my account?',
      answer:
          'Go to Settings → Danger Zone → Delete my account. Your data is '
          'retained for 90 days before permanent deletion.',
    ),
  ];
}
