'use strict';

const fs = require('fs');
const path = require('path');
const { item, folder, query, pathVar, validationErrorBody, unauthorizedBody } = require('./lib');

// ---------------------------------------------------------------------------------------
// Fixed sample IDs (also mirrored as example values on {{...}} vars so examples read naturally)
// ---------------------------------------------------------------------------------------
const IDS = {
  customer: '5f3d8e2a-0000-4000-8000-000000000001',
  businessStarbucks: '2b7c9a10-0000-4000-8000-000000000101',
  businessNike: '2b7c9a10-0000-4000-8000-000000000102',
  branch: '3c8da211-0000-4000-8000-000000000201',
  membership: '4d9eb322-0000-4000-8000-000000000301',
  wallet: '5eafc433-0000-4000-8000-000000000401',
  transaction: '6fb0d544-0000-4000-8000-000000000501',
  tierGold: '70c1e655-0000-4000-8000-000000000601',
  reward: '81d2f766-0000-4000-8000-000000000701',
  coupon: '92e30877-0000-4000-8000-000000000801',
  campaign: 'a3f41988-0000-4000-8000-000000000901',
  notification: 'b4052a99-0000-4000-8000-000000001001',
  device: 'c5163baa-0000-4000-8000-000000001101',
  referral: 'd6274cbb-0000-4000-8000-000000001201',
  achievement: 'e7385dcc-0000-4000-8000-000000001301',
  ticket: 'f8496edd-0000-4000-8000-000000001401',
  category: '095a7fee-0000-4000-8000-000000001501',
};

const business = {
  id: IDS.businessStarbucks,
  nameEn: 'Starbucks - Downtown',
  nameAr: 'ستاربكس - وسط البلد',
  categoryId: IDS.category,
  categoryNameEn: 'Cafe',
  categoryNameAr: 'مقهى',
  logoUrl: 'https://cdn.eksabli.com/logos/starbucks.png',
  descriptionEn: 'Handcrafted coffee, tea and more.',
  descriptionAr: 'قهوة وشاي مصنوعان يدويًا والمزيد.',
  website: 'https://starbucks.com',
  distanceKm: 1.2,
  isMember: true,
  isFollowing: true,
  activeCampaignsCount: 1,
  branchesCount: 12,
};

const branch = {
  id: IDS.branch,
  tenantId: IDS.businessStarbucks,
  name: 'Downtown Mall Branch',
  address: '123 King Fahd Rd, Riyadh',
  latitude: 24.7136,
  longitude: 46.6753,
  phone: '+966501234567',
  openingHours: { sun: '08:00-23:00', mon: '08:00-23:00', tue: '08:00-23:00', wed: '08:00-23:00', thu: '08:00-23:00', fri: '14:00-23:00', sat: '08:00-23:00' },
};

const membership = {
  id: IDS.membership,
  businessId: IDS.businessStarbucks,
  businessNameEn: business.nameEn,
  businessNameAr: business.nameAr,
  businessLogoUrl: business.logoUrl,
  joinedAt: '2025-11-02T09:15:00Z',
  status: 'Active',
  referredByMembershipId: null,
};

const tier = { id: IDS.tierGold, nameEn: 'Gold', nameAr: 'ذهبي', minLifetimePoints: 5000, multiplier: 1.5 };

const wallet = {
  membershipId: IDS.membership,
  businessId: IDS.businessStarbucks,
  businessNameEn: business.nameEn,
  businessNameAr: business.nameAr,
  balance: 1280,
  lifetimeEarned: 6420,
  lifetimeRedeemed: 5140,
  currentTier: tier,
  nextTier: { nameEn: 'Platinum', nameAr: 'بلاتيني', minLifetimePoints: 10000, pointsToNextTier: 3580 },
};

const pointsTransaction = {
  id: IDS.transaction,
  walletId: IDS.wallet,
  businessId: IDS.businessStarbucks,
  businessNameEn: business.nameEn,
  type: 'Earn',
  points: 120,
  source: 'Purchase',
  referenceId: IDS.campaign,
  description: 'Purchase at Downtown Mall Branch (2x Double Points Weekend)',
  expiresAt: '2026-11-03T00:00:00Z',
  createdAt: '2025-11-03T14:22:10Z',
};

const reward = {
  id: IDS.reward,
  businessId: IDS.businessStarbucks,
  businessNameEn: business.nameEn,
  nameEn: 'Free Grande Beverage',
  nameAr: 'مشروب غراندي مجاني',
  type: 'FreeProduct',
  pointsCost: 450,
  stockRemaining: 37,
  imageUrl: 'https://cdn.eksabli.com/rewards/grande-beverage.png',
  requiresManagerApproval: false,
  validFrom: '2025-10-01T00:00:00Z',
  validTo: '2026-03-31T23:59:59Z',
};

const coupon = {
  id: IDS.coupon,
  rewardId: IDS.reward,
  rewardNameEn: reward.nameEn,
  businessId: IDS.businessStarbucks,
  code: 'EKS-9F3K-7QRT',
  status: 'Issued',
  redemptionMode: 'Qr',
  qrToken: 'eyJhbGciOiJub25lIn0.redemption-token-single-use',
  issuedAt: '2025-11-05T10:00:00Z',
  expiresAt: '2025-11-05T10:05:00Z',
  redeemedAt: null,
  redeemedByEmployeeId: null,
  redeemedBranchId: null,
};

const campaign = {
  id: IDS.campaign,
  businessId: IDS.businessStarbucks,
  businessNameEn: business.nameEn,
  nameEn: 'Double Points Weekend',
  nameAr: 'عطلة نهاية الأسبوع بنقاط مضاعفة',
  type: 'DoublePoints',
  bannerImageUrl: 'https://cdn.eksabli.com/campaigns/double-points.png',
  startDate: '2025-11-07T00:00:00Z',
  endDate: '2025-11-09T23:59:59Z',
  status: 'Active',
};

const notification = {
  id: IDS.notification,
  businessId: IDS.businessStarbucks,
  businessNameEn: business.nameEn,
  campaignId: IDS.campaign,
  channel: 'Push',
  title: 'Double Points Weekend is here!',
  body: 'Earn 2x points on every purchase at Starbucks this weekend only.',
  isRead: false,
  sentAt: '2025-11-07T08:00:00Z',
};

const referral = {
  id: IDS.referral,
  referrerMembershipId: IDS.membership,
  businessId: IDS.businessStarbucks,
  refereeName: 'Sara Al-Amri',
  status: 'Completed',
  bonusPoints: 200,
  createdAt: '2025-10-20T12:00:00Z',
  completedAt: '2025-10-25T09:30:00Z',
};

const achievement = {
  id: IDS.achievement,
  nameEn: 'Coffee Enthusiast',
  nameAr: 'عاشق القهوة',
  descriptionEn: 'Visit the same business 10 times in a month',
  iconUrl: 'https://cdn.eksabli.com/achievements/coffee-enthusiast.png',
  awardedAt: '2025-10-15T00:00:00Z',
  businessId: IDS.businessStarbucks,
};

const supportTicket = {
  id: IDS.ticket,
  subject: 'Points missing from last purchase',
  status: 'Open',
  priority: 'Normal',
  businessId: IDS.businessStarbucks,
  createdAt: '2025-11-06T16:40:00Z',
  messages: [
    { id: '11111111-0000-4000-8000-000000000001', senderType: 'Customer', body: 'I made a purchase yesterday but only got half the points I expected.', createdAt: '2025-11-06T16:40:00Z' },
  ],
};

// ---------------------------------------------------------------------------------------
// Reusable list wrapper (ABP PagedResultDto shape)
// ---------------------------------------------------------------------------------------
const paged = (items, totalCount = items.length) => ({ totalCount, items });

const AUTH_PERMISSIONS_NOTE = 'Host-realm authenticated customer (any signed-in customer may act on their own data)';

// =========================================================================================
// 1. Authentication
// =========================================================================================
const authFolder = folder('Authentication', 'Host-realm (customer) auth: registration, OTP, password login, tokens. All endpoints are `noauth` — they either create a session or operate on one supplied via the request body/header. Login and Refresh Token save `accessToken`/`refreshToken` into collection variables automatically via their Tests scripts.', [
  item({
    name: 'Register',
    method: 'POST',
    pathSegments: ['auth', 'register'],
    auth: 'noauth',
    description: 'Creates a new Host-realm customer identity (`IdentityUser` with `TenantId = null`) plus its `CustomerProfile`. Phone number must be unique platform-wide since it is used for cross-tenant exact-match lookup at POS.',
    body: {
      phoneNumber: '+966501112222',
      email: 'sara.customer@example.com',
      password: 'P@ssw0rd!2026',
      firstName: 'Sara',
      lastName: 'Al-Amri',
      dateOfBirth: '1996-04-12',
      gender: 'Female',
      preferredLanguage: 'ar',
    },
    success: {
      status: 'Created', code: 201,
      body: { id: IDS.customer, phoneNumber: '+966501112222', email: 'sara.customer@example.com', firstName: 'Sara', lastName: 'Al-Amri', isPhoneVerified: false, createdAt: '2025-11-08T10:00:00Z' },
    },
    includeValidation: true,
    validationFields: [
      { member: 'phoneNumber', message: "'phoneNumber' is not a valid phone number." },
      { member: 'password', message: "Passwords must be at least 8 characters and contain a digit." },
    ],
    includeAuthErrors: false,
    errors: [
      { name: '409 Conflict - Phone already registered', status: 'Conflict', code: 409, body: { error: { code: 'Eksabli:PhoneAlreadyRegistered', message: 'This phone number is already registered.', details: null, data: {}, validationErrors: null } } },
    ],
  }),
  item({
    name: 'Send OTP',
    method: 'POST',
    pathSegments: ['auth', 'send-otp'],
    auth: 'noauth',
    description: 'Sends a short-lived, single-use OTP code via SMS to the given phone number. Backed by a Redis-cached code, same "cache-backed short-lived token" shape used elsewhere in this API (Excel download tokens, redemption tokens).',
    body: { phoneNumber: '+966501112222', purpose: 'Login' },
    success: { status: 'OK', code: 200, body: { sent: true, phoneNumber: '+966501112222', expiresInSeconds: 120, resendAvailableInSeconds: 30 } },
    includeValidation: true,
    validationFields: [{ member: 'phoneNumber', message: "'phoneNumber' is required." }],
    includeAuthErrors: false,
    errors: [
      { name: '429 Too Many Requests - OTP rate limit', status: 'Too Many Requests', code: 429, body: { error: { code: 'Eksabli:OtpRateLimited', message: 'Too many OTP requests. Please wait before requesting a new code.', details: 'Retry after 30 seconds.', data: {}, validationErrors: null } } },
    ],
  }),
  item({
    name: 'Verify OTP',
    method: 'POST',
    pathSegments: ['auth', 'verify-otp'],
    auth: 'noauth',
    description: 'Validates the OTP and, on success, issues tokens via a custom OpenIddict grant (OTP-backed, single-use, Redis-cached code — same shape as the Excel-download token pattern). Saves `accessToken`/`refreshToken` on success.',
    body: { phoneNumber: '+966501112222', code: '482913', deviceId: IDS.device, devicePlatform: 'iOS', pushToken: 'fcm-push-token-abc123' },
    success: {
      status: 'OK', code: 200,
      body: { accessToken: 'eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.mobile-access-token', refreshToken: 'a1b2c3d4-refresh-token-e5f6', tokenType: 'Bearer', expiresIn: 3600, isNewCustomer: false },
    },
    includeValidation: true,
    validationFields: [{ member: 'code', message: "'code' must be 6 digits." }],
    includeAuthErrors: false,
    errors: [
      { name: '400 Bad Request - Invalid or expired code', status: 'Bad Request', code: 400, body: { error: { code: 'Eksabli:InvalidOtp', message: 'The OTP code is invalid or has expired.', details: null, data: {}, validationErrors: null } } },
    ],
    testScriptLines: [
      "if (pm.response.code === 200) {",
      "    const json = pm.response.json();",
      "    pm.collectionVariables.set('accessToken', json.accessToken);",
      "    pm.collectionVariables.set('refreshToken', json.refreshToken);",
      "    pm.collectionVariables.set('tokenExpiresAt', Date.now() + (json.expiresIn * 1000));",
      "    pm.environment.set('accessToken', json.accessToken);",
      "    pm.environment.set('refreshToken', json.refreshToken);",
      "    pm.test('Access token saved', function () { pm.expect(json.accessToken).to.be.a('string'); });",
      "}",
    ],
  }),
  item({
    name: 'Login (password)',
    method: 'POST',
    pathSegments: ['auth', 'login'],
    auth: 'noauth',
    description: 'Password-based login for returning customers who set a password (fallback to OTP is preferred for first login). Internally wraps the OpenIddict token endpoint; Authorization Code + PKCE is used from the Flutter app itself, this simplified endpoint exists for server-to-server/testing flows.',
    body: { phoneNumber: '+966501112222', password: 'P@ssw0rd!2026', deviceId: IDS.device, devicePlatform: 'iOS' },
    success: {
      status: 'OK', code: 200,
      body: { accessToken: 'eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.mobile-access-token', refreshToken: 'a1b2c3d4-refresh-token-e5f6', tokenType: 'Bearer', expiresIn: 3600 },
    },
    includeValidation: true,
    validationFields: [{ member: 'phoneNumber', message: "'phoneNumber' is required." }],
    errors: [
      { name: '401 Unauthorized - Wrong credentials', status: 'Unauthorized', code: 401, body: { error: { code: 'Volo.Abp.Identity:InvalidCredentials', message: 'Invalid phone number or password.', details: null, data: {}, validationErrors: null } } },
    ],
    includeAuthErrors: false,
    testScriptLines: [
      "if (pm.response.code === 200) {",
      "    const json = pm.response.json();",
      "    pm.collectionVariables.set('accessToken', json.accessToken);",
      "    pm.collectionVariables.set('refreshToken', json.refreshToken);",
      "    pm.collectionVariables.set('tokenExpiresAt', Date.now() + (json.expiresIn * 1000));",
      "    pm.environment.set('accessToken', json.accessToken);",
      "    pm.environment.set('refreshToken', json.refreshToken);",
      "    pm.test('Access token saved', function () { pm.expect(json.accessToken).to.be.a('string'); });",
      "}",
    ],
  }),
  item({
    name: 'Refresh Token',
    method: 'POST',
    pathSegments: ['auth', 'refresh-token'],
    auth: 'noauth',
    description: 'Rotates the refresh token. Refresh tokens are bound to a `Device` record so a single stolen refresh token can be revoked per-device without logging out every device.',
    body: { refreshToken: '{{refreshToken}}' },
    success: {
      status: 'OK', code: 200,
      body: { accessToken: 'eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.new-mobile-access-token', refreshToken: 'b2c3d4e5-refresh-token-f6a7', tokenType: 'Bearer', expiresIn: 3600 },
    },
    includeAuthErrors: false,
    errors: [
      { name: '401 Unauthorized - Refresh token revoked/expired', status: 'Unauthorized', code: 401, body: { error: { code: 'Eksabli:RefreshTokenInvalid', message: 'This refresh token is invalid, expired, or has been revoked.', details: null, data: {}, validationErrors: null } } },
    ],
    testScriptLines: [
      "if (pm.response.code === 200) {",
      "    const json = pm.response.json();",
      "    pm.collectionVariables.set('accessToken', json.accessToken);",
      "    pm.collectionVariables.set('refreshToken', json.refreshToken);",
      "    pm.collectionVariables.set('tokenExpiresAt', Date.now() + (json.expiresIn * 1000));",
      "    pm.environment.set('accessToken', json.accessToken);",
      "    pm.environment.set('refreshToken', json.refreshToken);",
      "}",
    ],
  }),
  item({
    name: 'Forgot Password',
    method: 'POST',
    pathSegments: ['auth', 'forgot-password'],
    auth: 'noauth',
    description: 'Sends a password-reset OTP/link to the phone number or email on file. Always returns 200 regardless of whether the account exists, to avoid account enumeration.',
    body: { phoneNumber: '+966501112222' },
    success: { status: 'OK', code: 200, body: { sent: true, message: 'If an account exists for this phone number, a reset code has been sent.' } },
    includeAuthErrors: false,
    includeValidation: true,
    validationFields: [{ member: 'phoneNumber', message: "'phoneNumber' is required." }],
  }),
  item({
    name: 'Reset Password',
    method: 'POST',
    pathSegments: ['auth', 'reset-password'],
    auth: 'noauth',
    description: 'Completes the password reset using the OTP/reset code obtained from Forgot Password.',
    body: { phoneNumber: '+966501112222', code: '482913', newPassword: 'N3wP@ssw0rd!2026' },
    success: { status: 'OK', code: 200, body: { reset: true } },
    includeValidation: true,
    validationFields: [{ member: 'newPassword', message: "Passwords must be at least 8 characters and contain a digit." }],
    includeAuthErrors: false,
    errors: [
      { name: '400 Bad Request - Invalid or expired code', status: 'Bad Request', code: 400, body: { error: { code: 'Eksabli:InvalidOtp', message: 'The reset code is invalid or has expired.', details: null, data: {}, validationErrors: null } } },
    ],
  }),
  item({
    name: 'Logout',
    method: 'POST',
    pathSegments: ['auth', 'logout'],
    auth: 'bearer',
    description: 'Revokes the current device\'s refresh token (see Profile > Devices to log out a *different* device). Access tokens remain valid until natural expiry (15-60 min) since they are stateless.',
    body: { deviceId: IDS.device },
    success: { status: 'No Content', code: 204, body: null },
    permission: '(any authenticated customer)',
  }),
]);

// =========================================================================================
// 2. Profile
// =========================================================================================
const profileFolder = folder('Profile', 'The signed-in customer\'s own `IdentityUser` + `CustomerProfile`, plus registered `Device`s ("log out this device").', [
  item({
    name: 'Get My Profile',
    method: 'GET',
    pathSegments: ['auth', 'profile'],
    auth: 'bearer',
    success: {
      status: 'OK', code: 200,
      body: { id: IDS.customer, phoneNumber: '+966501112222', email: 'sara.customer@example.com', firstName: 'Sara', lastName: 'Al-Amri', dateOfBirth: '1996-04-12', gender: 'Female', preferredLanguage: 'ar', isPhoneVerified: true, memberSince: '2025-06-01T00:00:00Z' },
    },
    permission: '(any authenticated customer, own profile only)',
  }),
  item({
    name: 'Update My Profile',
    method: 'PUT',
    pathSegments: ['auth', 'profile'],
    auth: 'bearer',
    body: { firstName: 'Sara', lastName: 'Al-Amri', dateOfBirth: '1996-04-12', gender: 'Female', email: 'sara.customer@example.com', preferredLanguage: 'ar' },
    success: {
      status: 'OK', code: 200,
      body: { id: IDS.customer, phoneNumber: '+966501112222', email: 'sara.customer@example.com', firstName: 'Sara', lastName: 'Al-Amri', dateOfBirth: '1996-04-12', gender: 'Female', preferredLanguage: 'ar' },
    },
    includeValidation: true,
    validationFields: [{ member: 'email', message: "'email' is not a valid email address." }],
    permission: '(any authenticated customer, own profile only)',
  }),
  item({
    name: 'List My Devices',
    method: 'GET',
    pathSegments: ['auth', 'devices'],
    auth: 'bearer',
    description: 'Supports "log out this device" without ending every session — see System Architecture > Security.',
    success: {
      status: 'OK', code: 200,
      body: paged([
        { id: IDS.device, platform: 'iOS', appVersion: '1.4.2', lastActiveAt: '2025-11-08T09:00:00Z', isCurrent: true },
        { id: '11111111-2222-4000-8000-000000000001', platform: 'Android', appVersion: '1.3.0', lastActiveAt: '2025-10-20T18:12:00Z', isCurrent: false },
      ]),
    },
  }),
  item({
    name: 'Log Out Device',
    method: 'DELETE',
    pathSegments: ['auth', 'devices', ':deviceId'],
    opts: { pathVars: [pathVar('deviceId', IDS.device, 'Device to revoke')] },
    auth: 'bearer',
    success: { status: 'No Content', code: 204, body: null },
    includeNotFound: true, notFoundEntity: 'Devices.Device', notFoundIdExpr: '{{deviceId}}',
  }),
  item({
    name: 'Delete My Account',
    method: 'DELETE',
    pathSegments: ['auth', 'account'],
    auth: 'bearer',
    description: 'GDPR-style erasure request. Per Security design, "delete" freezes the account and any still-active `Membership`s immediately, then hard-deletes on a delay/confirmation per data-retention policy — never an instant hard-delete of a live financial ledger.',
    body: { reason: 'No longer using the app', confirmPhoneNumber: '+966501112222' },
    success: { status: 'Accepted', code: 202, body: { accountId: IDS.customer, status: 'PendingErasure', effectiveAt: '2025-12-08T00:00:00Z' } },
    includeValidation: true,
    validationFields: [{ member: 'confirmPhoneNumber', message: 'Confirmation phone number does not match the signed-in account.' }],
  }),
]);

// =========================================================================================
// 3. Stores (business discovery)
// =========================================================================================
const storesFolder = folder('Stores', 'Business discovery — public read (`/api/businesses/*`, discovery is unauthenticated so the app can show nearby offers before login). Follow/unfollow requires auth.', [
  item({
    name: 'List Stores',
    method: 'GET',
    pathSegments: ['stores'],
    opts: { queries: [query('categoryId', IDS.category, 'Filter by business category'), query('SkipCount', 0), query('MaxResultCount', 20), query('Sorting', 'nameEn asc')] },
    auth: 'noauth',
    success: { status: 'OK', code: 200, body: paged([business, { ...business, id: IDS.businessNike, nameEn: 'Nike - Riyadh Park', nameAr: 'نايك - رياض بارك', categoryNameEn: 'Sportswear', categoryNameAr: 'ملابس رياضية', isMember: false, isFollowing: false, activeCampaignsCount: 1 }], 47) },
  }),
  item({
    name: 'Nearby Stores',
    method: 'GET',
    pathSegments: ['stores', 'nearby'],
    opts: { queries: [query('latitude', 24.7136), query('longitude', 46.6753), query('radiusKm', 5), query('MaxResultCount', 20)] },
    auth: 'noauth',
    description: 'Geo search backed by Postgres + PostGIS (see Scalability). Returns stores ordered by distance.',
    success: { status: 'OK', code: 200, body: paged([{ ...business, distanceKm: 0.6 }]) },
    includeValidation: true,
    validationFields: [{ member: 'latitude', message: "'latitude' must be between -90 and 90." }],
  }),
  item({
    name: 'Search Stores',
    method: 'GET',
    pathSegments: ['stores', 'search'],
    opts: { queries: [query('q', 'starbucks'), query('MaxResultCount', 20)] },
    auth: 'noauth',
    success: { status: 'OK', code: 200, body: paged([business], 1) },
  }),
  item({
    name: 'Get Store Detail',
    method: 'GET',
    pathSegments: ['stores', ':id'],
    opts: { pathVars: [pathVar('id', '{{businessId}}', 'Business (tenant) id')] },
    auth: 'noauth',
    success: { status: 'OK', code: 200, body: { ...business, branches: [branch], activeCampaigns: [{ id: IDS.campaign, nameEn: campaign.nameEn, bannerImageUrl: campaign.bannerImageUrl }] } },
    includeNotFound: true, notFoundEntity: 'Businesses.BusinessProfile', notFoundIdExpr: '{{businessId}}',
  }),
  item({
    name: 'List Store Branches',
    method: 'GET',
    pathSegments: ['stores', ':id', 'branches'],
    opts: { pathVars: [pathVar('id', '{{businessId}}', 'Business (tenant) id')] },
    auth: 'noauth',
    success: { status: 'OK', code: 200, body: paged([branch]) },
    includeNotFound: true, notFoundEntity: 'Businesses.BusinessProfile', notFoundIdExpr: '{{businessId}}',
  }),
  item({
    name: 'Follow Store',
    method: 'POST',
    pathSegments: ['stores', ':id', 'follow'],
    opts: { pathVars: [pathVar('id', '{{businessId}}', 'Business (tenant) id')] },
    auth: 'bearer',
    description: 'Creates (or reactivates) a `Follow` row for this customer + business — the same entity used for the Favorites and Followers folders, see Database Design > Engagement & gamification.',
    success: { status: 'OK', code: 200, body: { businessId: '{{businessId}}', isFollowing: true, followedAt: '2025-11-08T10:00:00Z' } },
    includeNotFound: true, notFoundEntity: 'Businesses.BusinessProfile', notFoundIdExpr: '{{businessId}}',
  }),
  item({
    name: 'Unfollow Store',
    method: 'DELETE',
    pathSegments: ['stores', ':id', 'follow'],
    opts: { pathVars: [pathVar('id', '{{businessId}}', 'Business (tenant) id')] },
    auth: 'bearer',
    success: { status: 'No Content', code: 204, body: null },
    includeNotFound: true, notFoundEntity: 'Engagement.Follow', notFoundIdExpr: 'customerId={{userId}}, tenantId={{businessId}}',
  }),
]);

// =========================================================================================
// 4. Memberships
// =========================================================================================
const membershipsFolder = folder('Memberships', 'The customer <-> business relationship (`Membership`). Host-realm, scoped by `CustomerId` via `DataFilter.Disable<IMultiTenant>()` rather than a single tenant filter, since one customer spans many businesses.', [
  item({
    name: 'List My Memberships',
    method: 'GET',
    pathSegments: ['memberships'],
    opts: { queries: [query('status', 'Active', 'Active | Frozen', true), query('SkipCount', 0), query('MaxResultCount', 20)] },
    auth: 'bearer',
    success: { status: 'OK', code: 200, body: paged([membership]) },
  }),
  item({
    name: 'Join Business',
    method: 'POST',
    pathSegments: ['memberships'],
    auth: 'bearer',
    body: { businessId: '{{businessId}}', referralCode: 'SARA-REF-8821' },
    success: {
      status: 'Created', code: 201,
      body: { ...membership, id: '11111111-3333-4000-8000-000000000001', wallet: { balance: 0, lifetimeEarned: 0, lifetimeRedeemed: 0, currentTier: null } },
    },
    includeValidation: true,
    validationFields: [{ member: 'businessId', message: "'businessId' is required." }],
    errors: [
      { name: '409 Conflict - Already a member', status: 'Conflict', code: 409, body: { error: { code: 'Eksabli:AlreadyMember', message: 'You are already a member of this business.', details: null, data: {}, validationErrors: null } } },
    ],
    includeNotFound: true, notFoundEntity: 'Businesses.BusinessProfile', notFoundIdExpr: '{{businessId}}',
  }),
  item({
    name: 'Get Membership Detail',
    method: 'GET',
    pathSegments: ['memberships', ':id'],
    opts: { pathVars: [pathVar('id', IDS.membership, 'Membership id')] },
    auth: 'bearer',
    success: { status: 'OK', code: 200, body: { ...membership, wallet } },
    includeNotFound: true, notFoundEntity: 'Memberships.Membership', notFoundIdExpr: IDS.membership,
  }),
  item({
    name: 'Leave Business',
    method: 'DELETE',
    pathSegments: ['memberships', ':id'],
    opts: { pathVars: [pathVar('id', IDS.membership, 'Membership id')] },
    auth: 'bearer',
    description: 'Freezes the membership (`Status = Frozen`) rather than hard-deleting — the wallet/ledger for a frozen membership is retained for support/dispute history.',
    success: { status: 'No Content', code: 204, body: null },
    includeNotFound: true, notFoundEntity: 'Memberships.Membership', notFoundIdExpr: IDS.membership,
  }),
]);

// =========================================================================================
// 5. Points
// =========================================================================================
const pointsFolder = folder('Points', 'Quick balance summary + full ledger reads. `PointsWallet.Balance` is a denormalized cache; `PointsTransaction` is the append-only source of truth (see Database Design > Membership & wallet).', [
  item({
    name: 'My Points Summary (all businesses)',
    method: 'GET',
    pathSegments: ['points'],
    auth: 'bearer',
    success: {
      status: 'OK', code: 200,
      body: {
        totalBalanceAcrossBusinesses: 1280,
        businesses: [
          { businessId: IDS.businessStarbucks, businessNameEn: business.nameEn, balance: 1280, currentTier: tier },
          { businessId: IDS.businessNike, businessNameEn: 'Nike - Riyadh Park', balance: 340, currentTier: { nameEn: 'Silver', nameAr: 'فضي', minLifetimePoints: 1000, multiplier: 1.1 } },
        ],
      },
    },
  }),
  item({
    name: 'My Points History',
    method: 'GET',
    pathSegments: ['points', 'history'],
    opts: { queries: [query('businessId', '{{businessId}}', 'Filter to a single business', true), query('type', 'Earn', 'Earn|Redeem|Expire|Adjust|Refund', true), query('SkipCount', 0), query('MaxResultCount', 20)] },
    auth: 'bearer',
    success: { status: 'OK', code: 200, body: paged([pointsTransaction], 214) },
  }),
]);

// =========================================================================================
// 6. Rewards
// =========================================================================================
const rewardsFolder = folder('Rewards', 'Redemption catalog (customer read) + redeem action, which mints a `Coupon` using the same short-lived single-use token pattern already implemented for Excel downloads in this repo.', [
  item({
    name: 'List Rewards',
    method: 'GET',
    pathSegments: ['rewards'],
    opts: { queries: [query('businessId', '{{businessId}}', 'Filter to a single business', true), query('maxPointsCost', 1000, '', true), query('SkipCount', 0), query('MaxResultCount', 20)] },
    auth: 'bearer',
    success: { status: 'OK', code: 200, body: paged([reward, { ...reward, id: '22222222-4444-4000-8000-000000000001', nameEn: '10% Off Any Purchase', nameAr: 'خصم 10% على أي عملية شراء', type: 'Discount', pointsCost: 200 }], 9) },
  }),
  item({
    name: 'Get Reward Detail',
    method: 'GET',
    pathSegments: ['rewards', ':id'],
    opts: { pathVars: [pathVar('id', '{{rewardId}}', 'Reward id')] },
    auth: 'bearer',
    success: { status: 'OK', code: 200, body: { ...reward, id: '{{rewardId}}', descriptionEn: 'Any handcrafted Grande beverage, any customization.', descriptionAr: 'أي مشروب غراندي يدوي، مع أي تخصيص.', canAfford: true } },
    includeNotFound: true, notFoundEntity: 'Rewards.Reward', notFoundIdExpr: '{{rewardId}}',
  }),
  item({
    name: 'Redeem Reward',
    method: 'POST',
    pathSegments: ['rewards', ':id', 'redeem'],
    opts: { pathVars: [pathVar('id', '{{rewardId}}', 'Reward id')] },
    auth: 'bearer',
    description: 'Mints a `Coupon` (`Status = Issued`) with a short-lived signed QR token (or a PIN, if `mode` is `Pin`). Points are debited immediately as a `PointsTransaction` of `Type = Redeem`.',
    body: { membershipId: IDS.membership, mode: 'Qr' },
    success: { status: 'Created', code: 201, body: { ...coupon, rewardId: '{{rewardId}}' } },
    errors: [
      { name: '400 Bad Request - Insufficient points', status: 'Bad Request', code: 400, body: { error: { code: 'Eksabli:InsufficientPoints', message: 'You do not have enough points to redeem this reward.', details: 'Required: 450, available: 210.', data: { required: 450, available: 210 }, validationErrors: null } } },
      { name: '409 Conflict - Out of stock', status: 'Conflict', code: 409, body: { error: { code: 'Eksabli:RewardOutOfStock', message: 'This reward is out of stock.', details: null, data: {}, validationErrors: null } } },
    ],
    includeNotFound: true, notFoundEntity: 'Rewards.Reward', notFoundIdExpr: '{{rewardId}}',
  }),
]);

// =========================================================================================
// 7. Coupons
// =========================================================================================
const couponsFolder = folder('Coupons', 'Issued reward instances — redemption history + live QR/PIN for an unredeemed coupon.', [
  item({
    name: 'List My Coupons',
    method: 'GET',
    pathSegments: ['coupons'],
    opts: { queries: [query('status', 'Issued', 'Issued|Redeemed|Expired|Cancelled', true), query('businessId', '{{businessId}}', '', true), query('SkipCount', 0), query('MaxResultCount', 20)] },
    auth: 'bearer',
    success: { status: 'OK', code: 200, body: paged([coupon, { ...coupon, id: '33333333-5555-4000-8000-000000000001', status: 'Redeemed', redeemedAt: '2025-10-01T13:00:00Z', redeemedByEmployeeId: 'employee-guid', redeemedBranchId: IDS.branch }], 12) },
  }),
  item({
    name: 'Get Coupon Detail',
    method: 'GET',
    pathSegments: ['coupons', ':id'],
    opts: { pathVars: [pathVar('id', IDS.coupon, 'Coupon id')] },
    auth: 'bearer',
    description: 'Poll this while showing the redemption screen — `qrToken`/PIN and `expiresAt` refresh countdown until redeemed or expired.',
    success: { status: 'OK', code: 200, body: coupon },
    includeNotFound: true, notFoundEntity: 'Rewards.Coupon', notFoundIdExpr: IDS.coupon,
  }),
]);

// =========================================================================================
// 8. Campaigns
// =========================================================================================
const campaignsFolder = folder('Campaigns', 'Active promotions feed (customer read side of `/api/campaigns/*`). Creation/activation is Business API only.', [
  item({
    name: 'List Active Campaigns',
    method: 'GET',
    pathSegments: ['campaigns'],
    opts: { queries: [query('businessId', '{{businessId}}', '', true), query('SkipCount', 0), query('MaxResultCount', 20)] },
    auth: 'bearer',
    success: { status: 'OK', code: 200, body: paged([campaign, { ...campaign, id: '44444444-6666-4000-8000-000000000001', businessId: IDS.businessNike, businessNameEn: 'Nike - Riyadh Park', nameEn: '20% Off Everything', nameAr: 'خصم 20% على كل شيء', type: 'SpendXGetY' }]) },
  }),
  item({
    name: 'Get Campaign Detail',
    method: 'GET',
    pathSegments: ['campaigns', ':id'],
    opts: { pathVars: [pathVar('id', '{{campaignId}}', 'Campaign id')] },
    auth: 'bearer',
    success: { status: 'OK', code: 200, body: { ...campaign, id: '{{campaignId}}', descriptionEn: 'Every purchase earns double points, no minimum spend.', descriptionAr: 'كل عملية شراء تحصل على نقاط مضاعفة، بدون حد أدنى للإنفاق.' } },
    includeNotFound: true, notFoundEntity: 'Campaigns.Campaign', notFoundIdExpr: '{{campaignId}}',
  }),
]);

// =========================================================================================
// 9. Notifications
// =========================================================================================
const notificationsFolder = folder('Notifications', 'Delivery records addressed to this customer (`Notification.MembershipId`), plus channel preferences (Host side of `/api/notifications/*`).', [
  item({
    name: 'List My Notifications',
    method: 'GET',
    pathSegments: ['notifications'],
    opts: { queries: [query('unreadOnly', 'false', '', true), query('SkipCount', 0), query('MaxResultCount', 20)] },
    auth: 'bearer',
    success: { status: 'OK', code: 200, body: paged([notification], 34) },
  }),
  item({
    name: 'Mark Notifications Read',
    method: 'PUT',
    pathSegments: ['notifications', 'read'],
    auth: 'bearer',
    body: { notificationIds: [IDS.notification], markAll: false },
    success: { status: 'OK', code: 200, body: { updatedCount: 1 } },
    includeValidation: true,
    validationFields: [{ member: 'notificationIds', message: "Provide 'notificationIds' or set 'markAll' to true." }],
  }),
  item({
    name: 'Get Notification Preferences',
    method: 'GET',
    pathSegments: ['notifications', 'preferences'],
    auth: 'bearer',
    success: { status: 'OK', code: 200, body: { push: true, email: true, sms: false, campaignUpdates: true, transactionAlerts: true } },
  }),
  item({
    name: 'Update Notification Preferences',
    method: 'PUT',
    pathSegments: ['notifications', 'preferences'],
    auth: 'bearer',
    body: { push: true, email: false, sms: false, campaignUpdates: true, transactionAlerts: true },
    success: { status: 'OK', code: 200, body: { push: true, email: false, sms: false, campaignUpdates: true, transactionAlerts: true } },
  }),
]);

// =========================================================================================
// 10. Wallet
// =========================================================================================
const walletFolder = folder('Wallet', 'Cross-business wallet aggregate — mirrors the Home screen\'s wallet carousel. Runs as a Host user with `IMultiTenant` filtering explicitly disabled and replaced with a `CustomerId` filter (see System Architecture > Two identity realms).', [
  item({
    name: 'List All Wallets',
    method: 'GET',
    pathSegments: ['wallet'],
    auth: 'bearer',
    success: {
      status: 'OK', code: 200,
      body: paged([wallet, { membershipId: '11111111-7777-4000-8000-000000000001', businessId: IDS.businessNike, businessNameEn: 'Nike - Riyadh Park', businessNameAr: 'نايك - رياض بارك', balance: 340, lifetimeEarned: 900, lifetimeRedeemed: 560, currentTier: { nameEn: 'Silver', nameAr: 'فضي', minLifetimePoints: 1000, multiplier: 1.1 }, nextTier: { nameEn: 'Gold', nameAr: 'ذهبي', minLifetimePoints: 5000, pointsToNextTier: 4100 } }]),
    },
  }),
  item({
    name: 'Get Wallet Detail',
    method: 'GET',
    pathSegments: ['wallet', ':businessId'],
    opts: { pathVars: [pathVar('businessId', '{{businessId}}', 'Business (tenant) id')] },
    auth: 'bearer',
    success: { status: 'OK', code: 200, body: wallet },
    includeNotFound: true, notFoundEntity: 'Memberships.PointsWallet', notFoundIdExpr: 'customerId={{userId}}, tenantId={{businessId}}',
  }),
  item({
    name: 'Wallet Transactions (per business)',
    method: 'GET',
    pathSegments: ['wallet', ':businessId', 'transactions'],
    opts: { pathVars: [pathVar('businessId', '{{businessId}}', 'Business (tenant) id')], queries: [query('SkipCount', 0), query('MaxResultCount', 20)] },
    auth: 'bearer',
    description: 'Matches the architecture doc\'s `/api/wallet/{tenantId}/transactions` resource exactly — points history scoped to one business, filtered server-side by the caller\'s own `CustomerId`.',
    success: { status: 'OK', code: 200, body: paged([pointsTransaction], 214) },
    includeNotFound: true, notFoundEntity: 'Memberships.PointsWallet', notFoundIdExpr: 'customerId={{userId}}, tenantId={{businessId}}',
  }),
]);

// =========================================================================================
// 11. Transactions
// =========================================================================================
const transactionsFolder = folder('Transactions', 'Cross-business raw ledger view (union of every wallet\'s `PointsTransaction` rows for this customer) — the "show your work" screen behind the Points summary.', [
  item({
    name: 'List My Transactions',
    method: 'GET',
    pathSegments: ['transactions'],
    opts: { queries: [query('businessId', '{{businessId}}', '', true), query('type', 'Earn', 'Earn|Redeem|Expire|Adjust|Refund', true), query('dateFrom', '2025-10-01', '', true), query('dateTo', '2025-11-08', '', true), query('SkipCount', 0), query('MaxResultCount', 20)] },
    auth: 'bearer',
    success: { status: 'OK', code: 200, body: paged([pointsTransaction], 214) },
  }),
  item({
    name: 'Get Transaction Detail',
    method: 'GET',
    pathSegments: ['transactions', ':id'],
    opts: { pathVars: [pathVar('id', '{{transactionId}}', 'PointsTransaction id')] },
    auth: 'bearer',
    success: { status: 'OK', code: 200, body: { ...pointsTransaction, id: '{{transactionId}}' } },
    includeNotFound: true, notFoundEntity: 'Memberships.PointsTransaction', notFoundIdExpr: '{{transactionId}}',
  }),
]);

// =========================================================================================
// 12. Favorites
// =========================================================================================
const favoritesFolder = folder('Favorites', 'Businesses this customer has favorited without necessarily being a member. Backed by the same `Follow` entity as the Followers folder below (see Database Design: "Favorites and Followers are the same concept here") — Favorites is the customer-facing read of it.', [
  item({
    name: 'List My Favorites',
    method: 'GET',
    pathSegments: ['favorites'],
    opts: { queries: [query('SkipCount', 0), query('MaxResultCount', 20)] },
    auth: 'bearer',
    success: { status: 'OK', code: 200, body: paged([{ businessId: IDS.businessNike, businessNameEn: 'Nike - Riyadh Park', businessNameAr: 'نايك - رياض بارك', logoUrl: 'https://cdn.eksabli.com/logos/nike.png', isMember: false, followedAt: '2025-09-12T10:00:00Z' }]) },
  }),
]);

// =========================================================================================
// 13. Followers
// =========================================================================================
const followersFolder = folder('Followers', 'Same underlying `Follow` rows as Favorites, read as an activity feed: offers/campaigns from businesses you follow but have not joined yet. (Follow/Unfollow itself lives under Stores > Follow Store / Unfollow Store — this folder is read-only from the mobile side; the businessfacing "convert follower to campaign target" action lives in the Business API.)', [
  item({
    name: 'Followed Businesses Feed',
    method: 'GET',
    pathSegments: ['followers', 'feed'],
    opts: { queries: [query('SkipCount', 0), query('MaxResultCount', 20)] },
    auth: 'bearer',
    success: {
      status: 'OK', code: 200,
      body: paged([
        { businessId: IDS.businessNike, businessNameEn: 'Nike - Riyadh Park', type: 'Offer', title: 'Weekend Sale - 20% Off', publishedAt: '2025-11-06T09:00:00Z' },
      ]),
    },
  }),
]);

// =========================================================================================
// 14. Referrals
// =========================================================================================
const referralsFolder = folder('Referrals', '`Referral` tracks one customer inviting another into a specific business; completion pays a bonus to both referrer and referee via the points pipeline (`Source = Referral`).', [
  item({
    name: 'Generate Referral Code',
    method: 'POST',
    pathSegments: ['referrals'],
    auth: 'bearer',
    body: { businessId: '{{businessId}}' },
    success: { status: 'Created', code: 201, body: { referralCode: 'SARA-REF-8821', shareUrl: 'https://eksabli.app/r/SARA-REF-8821', businessId: '{{businessId}}' } },
    includeNotFound: true, notFoundEntity: 'Businesses.BusinessProfile', notFoundIdExpr: '{{businessId}}',
  }),
  item({
    name: 'List My Referrals',
    method: 'GET',
    pathSegments: ['referrals'],
    opts: { queries: [query('status', 'Completed', 'Pending|Completed|Rewarded', true), query('SkipCount', 0), query('MaxResultCount', 20)] },
    auth: 'bearer',
    success: { status: 'OK', code: 200, body: paged([referral]) },
  }),
]);

// =========================================================================================
// 15. Achievements
// =========================================================================================
const achievementsFolder = folder('Achievements', 'Badge definitions (platform-wide or tenant-specific) + which ones this customer has earned. Tenant-level opt-in feature (ABP Feature Management flag) — not every business enables this.', [
  item({
    name: 'List My Achievements',
    method: 'GET',
    pathSegments: ['achievements'],
    opts: { queries: [query('businessId', '{{businessId}}', 'Omit for platform-wide achievements', true)] },
    auth: 'bearer',
    success: {
      status: 'OK', code: 200,
      body: paged([
        { ...achievement, earned: true },
        { id: '55555555-8888-4000-8000-000000000001', nameEn: 'First Redemption', nameAr: 'أول عملية استبدال', descriptionEn: 'Redeem your first reward', iconUrl: 'https://cdn.eksabli.com/achievements/first-redemption.png', earned: false, awardedAt: null },
      ]),
    },
  }),
]);

// =========================================================================================
// 16. Leaderboard
// =========================================================================================
const leaderboardFolder = folder('Leaderboard', 'Per-business ranking by lifetime points, gated by the same tenant-opt-in Feature Management flag as Achievements (see Loyalty Engine > Customer engagement) — treat as an optional gamification surface, not a guaranteed-on core endpoint.', [
  item({
    name: 'Get Business Leaderboard',
    method: 'GET',
    pathSegments: ['leaderboard'],
    opts: { queries: [query('businessId', '{{businessId}}'), query('period', 'monthly', 'weekly|monthly|allTime'), query('MaxResultCount', 10)] },
    auth: 'bearer',
    success: {
      status: 'OK', code: 200,
      body: {
        businessId: '{{businessId}}', period: 'monthly', myRank: 8, myLifetimePoints: 6420,
        topEntries: [
          { rank: 1, displayName: 'Ahmed K.', lifetimePoints: 24800 },
          { rank: 2, displayName: 'Layla M.', lifetimePoints: 19650 },
        ],
      },
    },
    errors: [
      { name: '404 Not Found - Leaderboard not enabled', status: 'Not Found', code: 404, body: { error: { code: 'Eksabli:FeatureNotEnabled', message: 'This business has not enabled the Leaderboard feature.', details: null, data: {}, validationErrors: null } } },
    ],
  }),
]);

// =========================================================================================
// 17. Settings
// =========================================================================================
const settingsFolder = folder('Settings', 'App-level customer settings distinct from Profile (identity fields) — language and display preferences.', [
  item({
    name: 'Get My Settings',
    method: 'GET',
    pathSegments: ['settings'],
    auth: 'bearer',
    success: { status: 'OK', code: 200, body: { language: 'ar', theme: 'system', biometricLoginEnabled: true } },
  }),
  item({
    name: 'Update My Settings',
    method: 'PUT',
    pathSegments: ['settings'],
    auth: 'bearer',
    body: { language: 'en', theme: 'dark', biometricLoginEnabled: true },
    success: { status: 'OK', code: 200, body: { language: 'en', theme: 'dark', biometricLoginEnabled: true } },
    includeValidation: true,
    validationFields: [{ member: 'language', message: "'language' must be one of: ar, en." }],
  }),
]);

// =========================================================================================
// 18. Support
// =========================================================================================
const supportFolder = folder('Support', 'Customer-facing `SupportTicket`/`SupportTicketMessage` — the same tables the Admin API\'s Support Tickets queue reads from.', [
  item({
    name: 'Create Support Ticket',
    method: 'POST',
    pathSegments: ['support'],
    auth: 'bearer',
    body: { subject: 'Points missing from last purchase', body: 'I made a purchase yesterday but only got half the points I expected.', businessId: '{{businessId}}', priority: 'Normal' },
    success: { status: 'Created', code: 201, body: supportTicket },
    includeValidation: true,
    validationFields: [{ member: 'subject', message: "'subject' is required." }],
  }),
  item({
    name: 'List My Support Tickets',
    method: 'GET',
    pathSegments: ['support'],
    opts: { queries: [query('status', 'Open', 'Open|Pending|Resolved|Closed', true), query('SkipCount', 0), query('MaxResultCount', 20)] },
    auth: 'bearer',
    success: { status: 'OK', code: 200, body: paged([supportTicket]) },
  }),
  item({
    name: 'Get Support Ticket Thread',
    method: 'GET',
    pathSegments: ['support', ':id'],
    opts: { pathVars: [pathVar('id', IDS.ticket, 'SupportTicket id')] },
    auth: 'bearer',
    success: { status: 'OK', code: 200, body: supportTicket },
    includeNotFound: true, notFoundEntity: 'Platform.SupportTicket', notFoundIdExpr: IDS.ticket,
  }),
  item({
    name: 'Reply to Support Ticket',
    method: 'POST',
    pathSegments: ['support', ':id', 'messages'],
    opts: { pathVars: [pathVar('id', IDS.ticket, 'SupportTicket id')] },
    auth: 'bearer',
    body: { body: 'Following up - still hasn\'t been resolved.' },
    success: { status: 'Created', code: 201, body: { id: '66666666-9999-4000-8000-000000000001', ticketId: IDS.ticket, senderType: 'Customer', body: 'Following up - still hasn\'t been resolved.', createdAt: '2025-11-08T11:00:00Z' } },
    includeNotFound: true, notFoundEntity: 'Platform.SupportTicket', notFoundIdExpr: IDS.ticket,
  }),
]);

// =========================================================================================
// Collection assembly
// =========================================================================================
const collection = {
  info: {
    name: 'Eksabli Mobile API',
    description:
      'Customer-facing (Host-realm) API for the Eksabli mobile app (Flutter). Customers are a single global identity that can join unlimited businesses (`Tenant`s); each `Membership` + `PointsWallet` is independent per business. See docs/eksabli-loyalty-platform for the full design (System Architecture > Two identity realms is the key thing to read first).\n\n' +
      'Base URL: `{{baseUrl}}` (default `https://localhost:44330/api/v1`, matches `Eksabli.HttpApi.Host` + URL-segment API versioning).\n\n' +
      'Auth: Bearer `{{accessToken}}` at the collection level. Run **Authentication > Verify OTP** (or **Login (password)**) first — its Tests script saves `accessToken`/`refreshToken` automatically. A collection-level Pre-request script auto-refreshes the token when it is close to expiry.\n\n' +
      'Error shape follows ABP\'s standard `{ error: { code, message, details, data, validationErrors } }` wrapper.',
    schema: 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json',
  },
  auth: { type: 'bearer', bearer: [{ key: 'token', value: '{{accessToken}}', type: 'string' }] },
  event: [
    {
      listen: 'prerequest',
      script: {
        type: 'text/javascript',
        exec: [
          '// Auto-refresh the access token if it is missing or close to expiry.',
          'const expiresAt = Number(pm.collectionVariables.get("tokenExpiresAt") || 0);',
          'const now = Date.now();',
          'const skewMs = 60 * 1000;',
          'if (expiresAt && (now + skewMs) > expiresAt) {',
          '    const refreshToken = pm.collectionVariables.get("refreshToken");',
          '    const baseUrl = pm.collectionVariables.get("baseUrl") || pm.environment.get("baseUrl");',
          '    if (refreshToken && baseUrl) {',
          '        pm.sendRequest({',
          '            url: baseUrl + "/auth/refresh-token",',
          '            method: "POST",',
          '            header: { "Content-Type": "application/json" },',
          '            body: { mode: "raw", raw: JSON.stringify({ refreshToken }) }',
          '        }, function (err, res) {',
          '            if (!err && res.code === 200) {',
          '                const json = res.json();',
          '                pm.collectionVariables.set("accessToken", json.accessToken);',
          '                pm.collectionVariables.set("refreshToken", json.refreshToken);',
          '                pm.collectionVariables.set("tokenExpiresAt", Date.now() + (json.expiresIn * 1000));',
          '            }',
          '        });',
          '    }',
          '}',
        ],
      },
    },
  ],
  variable: [
    { key: 'baseUrl', value: 'https://localhost:44330/api/v1', type: 'string' },
    { key: 'accessToken', value: '', type: 'string' },
    { key: 'refreshToken', value: '', type: 'string' },
    { key: 'tokenExpiresAt', value: '0', type: 'string' },
    { key: 'businessId', value: IDS.businessStarbucks, type: 'string' },
    { key: 'branchId', value: IDS.branch, type: 'string' },
    { key: 'userId', value: IDS.customer, type: 'string' },
    { key: 'rewardId', value: IDS.reward, type: 'string' },
    { key: 'campaignId', value: IDS.campaign, type: 'string' },
    { key: 'transactionId', value: IDS.transaction, type: 'string' },
  ],
  item: [
    authFolder, profileFolder, storesFolder, membershipsFolder, pointsFolder, rewardsFolder,
    couponsFolder, campaignsFolder, notificationsFolder, walletFolder, transactionsFolder,
    favoritesFolder, followersFolder, referralsFolder, achievementsFolder, leaderboardFolder,
    settingsFolder, supportFolder,
  ],
};

const outPath = path.join(__dirname, '..', 'Eksabli-Mobile-API.postman_collection.json');
fs.writeFileSync(outPath, JSON.stringify(collection, null, 2));
console.log('Wrote', outPath);

// sanity: total request count
let total = 0;
for (const f of collection.item) total += f.item.length;
console.log('Folders:', collection.item.length, 'Requests:', total);
