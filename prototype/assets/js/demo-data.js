/* ==========================================================================
   Eksabli — demo / mock data shared across every prototype page.
   All names, businesses, and figures are fictional. Nothing here is fetched
   over the network — the prototype is fully self-contained.
   ========================================================================== */

window.EksabliDemoData = {
  currentCustomer: {
    id: 'cus_001', firstName: 'Layla', lastName: 'Haddad', initials: 'LH',
    email: 'layla.haddad@example.com', phone: '+971 50 123 4567',
    dateOfBirth: '1996-03-14', gender: 'Female', memberSince: '2024-11-02',
  },

  businesses: [
    { id: 'biz_1', name: 'Cedar & Bean Coffee', category: 'Coffee Shop', initials: 'CB', color: 'from-amber-500 to-amber-700', rating: 4.8, branches: 6, distanceKm: 0.4, following: false, member: true },
    { id: 'biz_2', name: 'Pulse Fitness Club', category: 'Fitness & Gym', initials: 'PF', color: 'from-rose-500 to-rose-700', rating: 4.6, branches: 3, distanceKm: 1.2, following: false, member: true },
    { id: 'biz_3', name: 'Bloom Beauty Bar', category: 'Beauty & Spa', initials: 'BB', color: 'from-pink-400 to-fuchsia-600', rating: 4.9, branches: 2, distanceKm: 2.1, following: true, member: false },
    { id: 'biz_4', name: 'Cornerstone Bookshop', category: 'Books & Stationery', initials: 'CS', color: 'from-emerald-500 to-emerald-700', rating: 4.7, branches: 4, distanceKm: 0.9, following: false, member: true },
    { id: 'biz_5', name: 'Skyline Electronics', category: 'Electronics', initials: 'SE', color: 'from-sky-500 to-sky-700', rating: 4.4, branches: 8, distanceKm: 3.5, following: true, member: false },
    { id: 'biz_6', name: 'Olive Grove Restaurant', category: 'Restaurant', initials: 'OG', color: 'from-lime-600 to-green-700', rating: 4.5, branches: 5, distanceKm: 1.7, following: false, member: false },
    { id: 'biz_7', name: 'Petal & Stem Florist', category: 'Florist', initials: 'PS', color: 'from-violet-500 to-purple-700', rating: 4.9, branches: 1, distanceKm: 0.6, following: false, member: false },
    { id: 'biz_8', name: 'Verve Sneaker Co.', category: 'Retail & Fashion', initials: 'VS', color: 'from-indigo-500 to-primary-700', rating: 4.3, branches: 5, distanceKm: 2.8, following: false, member: false },
  ],

  // Wallets — only for businesses the demo customer is a member of.
  memberships: [
    { businessId: 'biz_1', membershipId: 'mem_1', joinedAt: '2024-11-05', status: 'Active', balance: 1250, lifetimeEarned: 3400, lifetimeRedeemed: 2150, tier: 'Gold', tierProgressPct: 68, nextTier: 'Platinum', pointsToNextTier: 750 },
    { businessId: 'biz_2', membershipId: 'mem_2', joinedAt: '2025-01-18', status: 'Active', balance: 420, lifetimeEarned: 920, lifetimeRedeemed: 500, tier: 'Silver', tierProgressPct: 40, nextTier: 'Gold', pointsToNextTier: 580 },
    { businessId: 'biz_4', membershipId: 'mem_3', joinedAt: '2025-03-22', status: 'Active', balance: 95, lifetimeEarned: 95, lifetimeRedeemed: 0, tier: 'Bronze', tierProgressPct: 19, nextTier: 'Silver', pointsToNextTier: 405 },
  ],

  transactions: [
    { id: 'txn_1', businessId: 'biz_1', type: 'Earn', points: 45, source: 'Purchase', description: 'In-store purchase — Branch: Downtown', date: '2026-08-04T09:15:00' },
    { id: 'txn_2', businessId: 'biz_1', type: 'Earn', points: 20, source: 'Campaign', description: 'Double Points Weekend', date: '2026-08-02T13:40:00' },
    { id: 'txn_3', businessId: 'biz_1', type: 'Redeem', points: -500, source: 'Reward', description: 'Redeemed: Free Large Latte', date: '2026-07-29T08:02:00' },
    { id: 'txn_4', businessId: 'biz_2', type: 'Earn', points: 100, source: 'Purchase', description: 'Monthly membership visit', date: '2026-07-28T18:20:00' },
    { id: 'txn_5', businessId: 'biz_1', type: 'Adjust', points: 15, source: 'Manual', description: 'Goodwill adjustment — Support Agent', date: '2026-07-25T11:00:00' },
    { id: 'txn_6', businessId: 'biz_4', type: 'Earn', points: 95, source: 'Purchase', description: 'Book purchase — Branch: Main Street', date: '2026-07-20T16:45:00' },
    { id: 'txn_7', businessId: 'biz_2', type: 'Earn', points: 50, source: 'Referral', description: 'Referral bonus — friend joined', date: '2026-07-15T10:10:00' },
    { id: 'txn_8', businessId: 'biz_1', type: 'Expire', points: -30, source: 'Expiration', description: 'Points expired (12-month policy)', date: '2026-07-10T00:00:00' },
    { id: 'txn_9', businessId: 'biz_2', type: 'Earn', points: 100, source: 'Birthday', description: 'Birthday bonus points', date: '2026-06-28T09:00:00' },
    { id: 'txn_10', businessId: 'biz_1', type: 'Earn', points: 60, source: 'Purchase', description: 'In-store purchase — Branch: Marina', date: '2026-06-22T17:30:00' },
  ],

  rewards: [
    { id: 'rew_1', businessId: 'biz_1', name: 'Free Large Latte', type: 'FreeProduct', pointsCost: 500, stock: 42, image: '☕', validTo: '2026-12-31' },
    { id: 'rew_2', businessId: 'biz_1', name: '20% Off Any Order', type: 'Discount', pointsCost: 350, stock: null, image: '🏷️', validTo: '2026-12-31' },
    { id: 'rew_3', businessId: 'biz_1', name: '$25 Gift Card', type: 'GiftCard', pointsCost: 2000, stock: 8, image: '🎁', validTo: '2026-10-31' },
    { id: 'rew_4', businessId: 'biz_2', name: 'Free Personal Training Session', type: 'FreeProduct', pointsCost: 800, stock: 15, image: '🏋️', validTo: '2026-11-30' },
    { id: 'rew_5', businessId: 'biz_2', name: '1 Month Membership Extension', type: 'FreeProduct', pointsCost: 1500, stock: 5, image: '📅', validTo: '2026-12-31' },
    { id: 'rew_6', businessId: 'biz_4', name: '$10 Off Next Purchase', type: 'Discount', pointsCost: 150, stock: null, image: '📚', validTo: '2026-12-31' },
    { id: 'rew_7', businessId: 'biz_4', name: 'Free Bookmark & Tote Bag', type: 'FreeProduct', pointsCost: 90, stock: 120, image: '👜', validTo: '2026-12-31' },
  ],

  coupons: [
    { id: 'cpn_1', rewardId: 'rew_1', businessId: 'biz_1', code: 'CB-8F3K2Q', status: 'Redeemed', issuedAt: '2026-07-29T07:58:00', redeemedAt: '2026-07-29T08:02:00', branch: 'Downtown' },
    { id: 'cpn_2', rewardId: 'rew_2', businessId: 'biz_1', code: 'CB-9L2M7R', status: 'Issued', issuedAt: '2026-08-04T10:00:00', redeemedAt: null, branch: null },
    { id: 'cpn_3', rewardId: 'rew_6', businessId: 'biz_4', code: 'CS-4T7Y1X', status: 'Expired', issuedAt: '2026-05-01T09:00:00', redeemedAt: null, branch: null },
  ],

  campaigns: [
    { id: 'cam_1', businessId: 'biz_1', name: 'Double Points Weekend', type: 'DoublePoints', status: 'Active', startDate: '2026-08-01', endDate: '2026-08-09', description: 'Earn 2x points on every purchase this weekend.', sent: 3400, opened: 2100, redeemed: 540 },
    { id: 'cam_2', businessId: 'biz_8', name: '20% Off Sneakers', type: 'Discount', status: 'Active', startDate: '2026-08-01', endDate: '2026-08-15', description: 'Flat 20% off all sneaker styles.', sent: 5200, opened: 3300, redeemed: 890 },
    { id: 'cam_3', businessId: 'biz_1', name: 'Birthday Bonus', type: 'Birthday', status: 'Active', startDate: '2026-01-01', endDate: '2026-12-31', description: '100 bonus points during your birthday month.', sent: 1200, opened: 980, redeemed: 610 },
    { id: 'cam_4', businessId: 'biz_2', name: 'Win-Back: We Miss You', type: 'WinBack', status: 'Draft', startDate: '2026-08-10', endDate: '2026-08-24', description: 'Targeting members inactive 60+ days.', sent: 0, opened: 0, redeemed: 0 },
    { id: 'cam_5', businessId: 'biz_4', name: 'New Member Welcome', type: 'NewCustomer', status: 'Active', startDate: '2026-01-01', endDate: '2026-12-31', description: 'Welcome bonus for members joined within 7 days.', sent: 860, opened: 790, redeemed: 705 },
    { id: 'cam_6', businessId: 'biz_1', name: 'Refer a Friend', type: 'Referral', status: 'Ended', startDate: '2026-05-01', endDate: '2026-06-30', description: 'Bonus points for referrer and referee.', sent: 2100, opened: 1450, redeemed: 380 },
  ],

  notifications: [
    { id: 'not_1', businessId: 'biz_1', channel: 'Push', title: 'Double Points Weekend is live!', body: 'Earn 2x points on every order through Sunday.', sentAt: '2026-08-04T08:00:00', read: false },
    { id: 'not_2', businessId: 'biz_2', channel: 'Push', title: 'Your Gold tier is close', body: 'Just 580 points to reach Gold at Pulse Fitness Club.', sentAt: '2026-08-03T15:20:00', read: false },
    { id: 'not_3', businessId: 'biz_1', channel: 'InApp', title: 'Coupon redeemed', body: 'You redeemed Free Large Latte at Downtown branch.', sentAt: '2026-07-29T08:02:00', read: true },
    { id: 'not_4', businessId: 'biz_4', channel: 'Email', title: 'Welcome to Cornerstone Bookshop', body: 'Thanks for joining — here is 90 bonus points to start.', sentAt: '2026-07-22T09:00:00', read: true },
    { id: 'not_5', businessId: 'biz_8', channel: 'Push', title: 'Verve Sneaker Co. — 20% off', body: 'A new offer just dropped for followers.', sentAt: '2026-08-02T12:00:00', read: true },
  ],

  referral: {
    code: 'LAYLA25', link: 'https://eksabli.app/r/LAYLA25',
    invited: 8, joined: 5, pointsEarned: 250,
    history: [
      { name: 'Omar S.', status: 'Rewarded', date: '2026-07-15' },
      { name: 'Nadia K.', status: 'Rewarded', date: '2026-06-30' },
      { name: 'Yousef A.', status: 'Pending', date: '2026-08-01' },
      { name: 'Rana M.', status: 'Completed', date: '2026-07-28' },
    ],
  },

  // -------------------- Business Portal (Tenant realm: Cedar & Bean Coffee) --------------------
  business: {
    profile: { name: 'Cedar & Bean Coffee', category: 'Coffee Shop', plan: 'Growth', ownerName: 'Marcus Reid', logoInitials: 'CB' },
    stats: {
      activeMembers: 3248, memberGrowthPct: 12.4,
      pointsIssued: 84200, pointsRedeemed: 51300,
      activeCampaigns: 2, lowStockRewards: 1,
      redemptionRatePct: 60.9, mrr: 4980,
    },
    branches: [
      { id: 'br_1', name: 'Downtown', address: '14 Al Wasl Rd, Dubai', phone: '+971 4 221 0098', members: 1120, status: 'Active' },
      { id: 'br_2', name: 'Marina', address: 'Marina Walk, Dubai', phone: '+971 4 221 0099', members: 940, status: 'Active' },
      { id: 'br_3', name: 'JBR', address: 'The Beach, JBR, Dubai', phone: '+971 4 221 0100', members: 610, status: 'Active' },
      { id: 'br_4', name: 'Business Bay', address: 'Bay Square, Dubai', phone: '+971 4 221 0101', members: 388, status: 'Active' },
      { id: 'br_5', name: 'Al Ain', address: 'Al Jimi Mall, Al Ain', phone: '+971 3 221 0102', members: 140, status: 'Inactive' },
      { id: 'br_6', name: 'Sharjah City Centre', address: 'City Centre, Sharjah', phone: '+971 6 221 0103', members: 50, status: 'Active' },
    ],
    employees: [
      { id: 'emp_1', name: 'Marcus Reid', role: 'Business Owner', branch: 'All branches', email: 'marcus@cedarbean.example', status: 'Active' },
      { id: 'emp_2', name: 'Sara Al Mansoori', role: 'Branch Manager', branch: 'Downtown', email: 'sara@cedarbean.example', status: 'Active' },
      { id: 'emp_3', name: 'Ahmed Farouk', role: 'Cashier', branch: 'Downtown', email: 'ahmed@cedarbean.example', status: 'Active' },
      { id: 'emp_4', name: 'Priya Nair', role: 'Marketing Manager', branch: 'All branches', email: 'priya@cedarbean.example', status: 'Active' },
      { id: 'emp_5', name: 'Karim Youssef', role: 'Cashier', branch: 'Marina', email: 'karim@cedarbean.example', status: 'Invited' },
      { id: 'emp_6', name: 'Elena Petrova', role: 'Cashier', branch: 'JBR', email: 'elena@cedarbean.example', status: 'Suspended' },
    ],
    customers: [
      { id: 'cus_1', name: 'Layla Haddad', phone: '+971 50 123 4567', tier: 'Gold', balance: 1250, joined: '2024-11-05', lastActive: '2026-08-04', status: 'Active' },
      { id: 'cus_2', name: 'Omar Suleiman', phone: '+971 50 234 5678', tier: 'Silver', balance: 640, joined: '2025-02-10', lastActive: '2026-08-01', status: 'Active' },
      { id: 'cus_3', name: 'Nadia Khalil', phone: '+971 50 345 6789', tier: 'Bronze', balance: 120, joined: '2025-06-19', lastActive: '2026-06-02', status: 'At Risk' },
      { id: 'cus_4', name: 'Yousef Attia', phone: '+971 50 456 7890', tier: 'Gold', balance: 2980, joined: '2024-08-01', lastActive: '2026-08-03', status: 'Active' },
      { id: 'cus_5', name: 'Rana Mostafa', phone: '+971 50 567 8901', tier: 'Silver', balance: 410, joined: '2025-01-28', lastActive: '2026-07-30', status: 'Active' },
      { id: 'cus_6', name: 'Hassan Idris', phone: '+971 50 678 9012', tier: 'Bronze', balance: 40, joined: '2025-11-11', lastActive: '2026-04-14', status: 'Churned' },
      { id: 'cus_7', name: 'Farah Zaidan', phone: '+971 50 789 0123', tier: 'Platinum', balance: 5100, joined: '2024-03-15', lastActive: '2026-08-04', status: 'Active' },
      { id: 'cus_8', name: 'Tariq Odeh', phone: '+971 50 890 1234', tier: 'Silver', balance: 300, joined: '2025-09-02', lastActive: '2026-07-18', status: 'Active' },
    ],
    pointRules: [
      { id: 'pr_1', ruleType: 'PerCurrencyUnit', pointsPerUnit: 1, label: '1 point per $1 spent', active: true },
      { id: 'pr_2', ruleType: 'PerVisit', pointsPerUnit: 10, label: '10 flat points per visit', active: false },
    ],
    tiers: [
      { name: 'Bronze', minPoints: 0, multiplier: 1.0 },
      { name: 'Silver', minPoints: 500, multiplier: 1.1 },
      { name: 'Gold', minPoints: 2000, multiplier: 1.25 },
      { name: 'Platinum', minPoints: 5000, multiplier: 1.5 },
    ],
    subscription: {
      plan: 'Growth', status: 'Active', renewalDate: '2026-09-01', monthlyPrice: 149,
      usage: { branches: { used: 6, limit: 5 }, activeMembers: { used: 3248, limit: 5000 }, campaigns: { used: 2, limit: null }, smsCredits: { used: 640, limit: 1000 } },
    },
    invoices: [
      { id: 'inv_1', period: 'Jul 2026', amount: 149, status: 'Paid', paidAt: '2026-07-01' },
      { id: 'inv_2', period: 'Jun 2026', amount: 149, status: 'Paid', paidAt: '2026-06-01' },
      { id: 'inv_3', period: 'May 2026', amount: 149, status: 'Paid', paidAt: '2026-05-02' },
      { id: 'inv_4', period: 'Aug 2026', amount: 178, status: 'Due', paidAt: null },
    ],
    monthlyGrowth: [
      { label: 'Feb', value: 2450 }, { label: 'Mar', value: 2610 }, { label: 'Apr', value: 2790 },
      { label: 'May', value: 2920 }, { label: 'Jun', value: 3050 }, { label: 'Jul', value: 3180 }, { label: 'Aug', value: 3248 },
    ],
  },

  // -------------------- Admin Portal (Host realm) --------------------
  admin: {
    stats: { totalTenants: 214, activeTenants: 189, trialingTenants: 25, mrr: 62400, dau: 18400, mau: 74200, openTickets: 12 },
    tenants: [
      { id: 'ten_1', name: 'Cedar & Bean Coffee', category: 'Coffee Shop', plan: 'Growth', status: 'Active', signupDate: '2024-10-20', members: 3248 },
      { id: 'ten_2', name: 'Pulse Fitness Club', category: 'Fitness & Gym', plan: 'Scale', status: 'Active', signupDate: '2024-06-11', members: 8120 },
      { id: 'ten_3', name: 'Bloom Beauty Bar', category: 'Beauty & Spa', plan: 'Starter', status: 'Pending Approval', signupDate: '2026-08-01', members: 0 },
      { id: 'ten_4', name: 'Cornerstone Bookshop', category: 'Books & Stationery', plan: 'Growth', status: 'Active', signupDate: '2025-01-05', members: 1900 },
      { id: 'ten_5', name: 'Skyline Electronics', category: 'Electronics', plan: 'Enterprise', status: 'Active', signupDate: '2023-11-30', members: 24500 },
      { id: 'ten_6', name: 'Olive Grove Restaurant', category: 'Restaurant', plan: 'Starter', status: 'Trialing', signupDate: '2026-07-25', members: 60 },
      { id: 'ten_7', name: 'Verve Sneaker Co.', category: 'Retail & Fashion', plan: 'Growth', status: 'Suspended', signupDate: '2024-04-14', members: 4100 },
    ],
    plans: [
      { name: 'Starter', price: 0, branches: 1, members: 500, campaigns: 1, notifications: 'In-app + Email', tenants: 34 },
      { name: 'Growth', price: 149, branches: 5, members: 5000, campaigns: 'Unlimited', notifications: '+ Push, limited SMS', tenants: 96 },
      { name: 'Scale', price: 449, branches: 25, members: 50000, campaigns: 'Unlimited + segmentation', notifications: 'SMS at cost, API', tenants: 61 },
      { name: 'Enterprise', price: null, branches: 'Unlimited', members: 'Custom', campaigns: 'Custom, white-label', notifications: 'Dedicated sender IDs', tenants: 23 },
    ],
    categories: [
      { name: 'Coffee Shop', businesses: 28 }, { name: 'Fitness & Gym', businesses: 19 }, { name: 'Beauty & Spa', businesses: 22 },
      { name: 'Books & Stationery', businesses: 11 }, { name: 'Electronics', businesses: 14 }, { name: 'Restaurant', businesses: 41 },
      { name: 'Florist', businesses: 8 }, { name: 'Retail & Fashion', businesses: 31 },
    ],
    supportTickets: [
      { id: 'tkt_1', subject: 'Cannot redeem coupon at POS', from: 'Cedar & Bean Coffee', type: 'Business', priority: 'High', status: 'Open', updatedAt: '2026-08-04T10:00:00' },
      { id: 'tkt_2', subject: 'Missing points from last purchase', from: 'Layla Haddad', type: 'Customer', priority: 'Medium', status: 'Open', updatedAt: '2026-08-03T16:30:00' },
      { id: 'tkt_3', subject: 'Upgrade billing question', from: 'Cornerstone Bookshop', type: 'Business', priority: 'Low', status: 'Pending', updatedAt: '2026-08-02T09:15:00' },
      { id: 'tkt_4', subject: 'Account deletion request', from: 'Hassan Idris', type: 'Customer', priority: 'Medium', status: 'Resolved', updatedAt: '2026-07-30T12:00:00' },
    ],
    auditLogs: [
      { id: 'log_1', actor: 'marcus@cedarbean.example', action: 'Manual point adjustment (+15)', target: 'Layla Haddad', timestamp: '2026-07-25T11:00:00' },
      { id: 'log_2', actor: 'support.jane@eksabli.app', action: 'View-as-tenant session', target: 'Cedar & Bean Coffee', timestamp: '2026-08-01T14:22:00' },
      { id: 'log_3', actor: 'admin.omar@eksabli.app', action: 'Approved new tenant', target: 'Olive Grove Restaurant', timestamp: '2026-07-25T08:40:00' },
      { id: 'log_4', actor: 'billing.rana@eksabli.app', action: 'Issued refund ($149)', target: 'Verve Sneaker Co.', timestamp: '2026-07-20T17:05:00' },
    ],
    featureFlags: [
      { key: 'Eksabli.SMSNotifications', label: 'SMS Notifications', enabled: true, rollout: '100%' },
      { key: 'Eksabli.Gamification.Achievements', label: 'Achievements / Badges', enabled: false, rollout: '0%' },
      { key: 'Eksabli.SelfServeSignup', label: 'Self-Serve Tenant Signup', enabled: false, rollout: '10% (beta)' },
      { key: 'Eksabli.PostGISDiscovery', label: 'PostGIS Nearby Search', enabled: true, rollout: '100%' },
    ],
    mrrTrend: [
      { label: 'Feb', value: 41200 }, { label: 'Mar', value: 45800 }, { label: 'Apr', value: 49500 },
      { label: 'May', value: 53100 }, { label: 'Jun', value: 57400 }, { label: 'Jul', value: 60200 }, { label: 'Aug', value: 62400 },
    ],
  },
};
