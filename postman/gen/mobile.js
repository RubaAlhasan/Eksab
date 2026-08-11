'use strict';

// Rebuilt from the LIVE Swagger doc (https://localhost:44330/swagger/v1/swagger.json) of the actual
// running Eksabli.HttpApi.Host, the real [Authorize]/permission attributes found on the hand-written
// controllers in src/Eksabli.HttpApi/Controllers/ (ABP auto-API-controllers are disabled here via
// [RemoteService(IsEnabled = false)] on every app service), AND live curl verification against the
// running host with a throwaway registered user (postman.smoketest) — response shapes/status codes
// below are observed, not guessed, wherever a verification was feasible without side effects that
// matter (see inline notes for what was actually exercised).
//
// Folders/endpoints the docs describe but that have no real route yet (store discovery/search, a
// customer notification inbox, campaigns/offers reads open to customers, leaderboard, app-level
// settings) are intentionally left out — see the collection description for the full gap list
// instead of inventing routes that would 404.

const fs = require('fs');
const path = require('path');
const { item, folder, query, pathVar, notFoundBody, businessRuleBody } = require('./lib');
const { enumVal, enumDoc } = require('./enums');

const IDS = {
  customer: '5f3d8e2a-0000-4000-8000-000000000001',
  tenantStarbucks: '2b7c9a10-0000-4000-8000-000000000101',
  membership: '4d9eb322-0000-4000-8000-000000000301',
  reward: '81d2f766-0000-4000-8000-000000000701',
  coupon: '92e30877-0000-4000-8000-000000000801',
  device: 'c5163baa-0000-4000-8000-000000001101',
  achievement: 'e7385dcc-0000-4000-8000-000000001301',
  ticket: 'f8496edd-0000-4000-8000-000000001401',
  category: '095a7fee-0000-4000-8000-000000001501',
  transaction: '6fb0d544-0000-4000-8000-000000000501',
  userId: '0a1b2c3d-4e5f-4000-8000-000000009999',
};

const PLACEHOLDER_NOTE = 'Placeholder GUID for illustration only — replace with a real id returned by ' +
  'Join Business / My Memberships (this backend has no seed data for Author/Book-style tutorial fixtures once Books/Authors were removed). ' +
  'Note: Join Business does NOT currently validate that the tenantId exists (verified live) — a bogus id still returns 200 with a Membership row, so a 404 here is aspirational, not observed.';

const TOKEN_RESPONSE_EXAMPLE = {
  access_token: 'eyJhbGciOiJSUzI1NiIsImtpZCI6IjhCMTk3NjcyMEMyMDg5QzYzQUFDREM2RUZDMTczNUY5MUJEMEZEOTMiLCJ4NXQiOiJpeGwyY2d3Z2ljWTZyTnh1X0JjMS1SdlFfWk0iLCJ0eXAiOiJhdCtqd3QifQ.mobile-access-token-truncated-for-readability',
  token_type: 'Bearer',
  expires_in: 3599,
  scope: 'Eksabli offline_access profile email phone roles',
  refresh_token: 'eyJhbGciOiJSU0EtT0FFUCIsImVuYyI6IkEyNTZDQkMtSFM1MTIi...refresh-token-truncated',
};

const tokenTestScript = [
  "if (pm.response.code === 200) {",
  "    const json = pm.response.json();",
  "    pm.collectionVariables.set('accessToken', json.access_token);",
  "    if (json.refresh_token) { pm.collectionVariables.set('refreshToken', json.refresh_token); }",
  "    pm.collectionVariables.set('tokenExpiresAt', Date.now() + (json.expires_in * 1000));",
  "    pm.environment.set('accessToken', json.access_token);",
  "    if (json.refresh_token) { pm.environment.set('refreshToken', json.refresh_token); }",
  "    pm.test('Access token saved', function () { pm.expect(json.access_token).to.be.a('string'); });",
  "}",
];

const oidcErrorExamples = (extra = []) => [
  { name: '400 Bad Request - invalid_grant', status: 'Bad Request', code: 400, body: { error: 'invalid_grant', error_description: 'The provided authorization grant is invalid, expired, or was issued to another client.' } },
  ...extra,
];

const paged = (items, totalCount = items.length) => ({ totalCount, items });

// =========================================================================================
// 1. Authentication
// =========================================================================================
const authFolder = folder(
  'Authentication',
  'Real token issuance goes through OpenIddict\'s own `/connect/token` endpoint (form-urlencoded, OAuth2 shape) — it is NOT an ABP application service, so it never appears in Swagger, but it is the actual mechanism a Bearer-token client (Flutter, or this collection) uses to sign in. Client: `Eksabli_App` (public, no secret; see src/Eksabli.Domain/OpenIddict/OpenIddictDataSeedContributor.cs and src/Eksabli.HttpApi.Host/OpenIddict/OtpLoginGrantHandler.cs). Verified live: Register -> Login (password grant) -> Bearer-authenticated calls all confirmed working end-to-end against this host. `/api/account/login` and `/api/account/check-password` also exist but are ABP\'s cookie-based MVC login (used by server-rendered pages), not a Bearer-issuing endpoint — not included below since it is not the path a mobile client uses.',
  [
    item({
      name: 'Send OTP',
      method: 'POST', pathSegments: ['app', 'otp', 'request'], auth: 'noauth',
      description: '`[AllowAnonymous]`. Sends a short-lived SMS code. Follow with **Login (OTP grant)** below, which redeems the code directly at the token endpoint (there is no separate "verify" REST call — OpenIddict\'s `otp` grant IS the verification step). Verified live: returns 204 No Content.',
      body: { phoneNumber: '+966501112222' },
      success: { status: 'No Content', code: 204, body: null },
      include401: false,
      includeValidation: true, validationFields: [{ member: 'phoneNumber', message: "'phoneNumber' is required." }],
    }),
    item({
      name: 'Login (OTP grant)',
      method: 'POST', pathSegments: ['connect', 'token'], auth: 'noauth',
      opts: { baseVar: 'issuerUrl' },
      description: 'Custom OpenIddict extension grant `grant_type=otp` (src/Eksabli.HttpApi.Host/OpenIddict/OtpLoginGrantHandler.cs) — exchanges a verified phone+code pair for tokens. Body MUST be `application/x-www-form-urlencoded` (standard OAuth2 token request), not JSON.\n\n' +
        'Two cases, same call: (1) phone number never registered at all -> creates a bare Host-realm customer on the spot (legacy "pure OTP login" fallback, e.g. for numbers added via POS). (2) phone number was registered via **Register Customer** above but not yet verified -> this call is what actually activates that account (`phoneNumberConfirmed` flips true) and logs it in; the profile/name/email/dob captured at registration was already saved, this step only proves the phone number. Already-verified accounts just log in normally.',
      formBody: [
        { key: 'grant_type', value: 'otp' },
        { key: 'phone_number', value: '+966501112222' },
        { key: 'otp_code', value: '482913' },
        { key: 'client_id', value: 'Eksabli_App' },
        { key: 'scope', value: 'Eksabli offline_access profile email phone roles' },
      ],
      success: { status: 'OK', code: 200, body: TOKEN_RESPONSE_EXAMPLE },
      include401: false,
      errors: oidcErrorExamples([{ name: '400 Bad Request - code expired', status: 'Bad Request', code: 400, body: { error: 'invalid_grant', error_description: 'The code has expired.' } }]),
      testScriptLines: tokenTestScript,
    }),
    item({
      name: 'Login (password grant)',
      method: 'POST', pathSegments: ['connect', 'token'], auth: 'noauth',
      opts: { baseVar: 'issuerUrl' },
      description: 'Standard OAuth2 Resource Owner Password grant. `username` is the ABP `IdentityUser.UserName` — for a customer created via **Register Customer**, that\'s the normalized phone number itself (not a chosen handle), since phone IS the username by this app\'s design. Requires `offline_access` in `scope` to receive a `refresh_token`. Note: the account must have `phoneNumberConfirmed: true` (i.e. completed **Login (OTP grant)** at least once) — password-only login for a still-unverified registration has not been tested and is not guaranteed to work.',
      formBody: [
        { key: 'grant_type', value: 'password' },
        { key: 'username', value: '+966501112222' },
        { key: 'password', value: 'P@ssw0rd!2026' },
        { key: 'client_id', value: 'Eksabli_App' },
        { key: 'scope', value: 'Eksabli offline_access profile email phone roles' },
      ],
      success: { status: 'OK', code: 200, body: TOKEN_RESPONSE_EXAMPLE },
      include401: false,
      errors: oidcErrorExamples(),
      testScriptLines: tokenTestScript,
    }),
    item({
      name: 'Refresh Token',
      method: 'POST', pathSegments: ['connect', 'token'], auth: 'noauth',
      opts: { baseVar: 'issuerUrl' },
      description: 'Rotates the access/refresh token pair. `refresh_token` scope must have been granted at login for this to work. Verified live (200, new access/refresh token pair returned).',
      formBody: [
        { key: 'grant_type', value: 'refresh_token' },
        { key: 'refresh_token', value: '{{refreshToken}}' },
        { key: 'client_id', value: 'Eksabli_App' },
      ],
      success: { status: 'OK', code: 200, body: TOKEN_RESPONSE_EXAMPLE },
      include401: false,
      errors: oidcErrorExamples(),
      testScriptLines: tokenTestScript,
    }),
    item({
      name: 'Register Customer',
      method: 'POST', pathSegments: ['app', 'otp', 'register'], auth: 'noauth',
      description: 'Field set matches `prototype/customer/register.html` exactly. Creates the Host-realm `IdentityUser` **and** a fully-populated `CustomerProfile` (name/email/dob/gender) immediately — not blank like the old stock self-registration below used to leave it — but the account stays unusable (`phoneNumberConfirmed: false`) until you follow up with **Login (OTP grant)** using the code this call sends (same SMS mechanism as **Send OTP**, no separate request needed). Posting the same, still-unverified phone number again overwrites the earlier attempt rather than erroring (handles an abandoned/retry signup); posting an ALREADY-VERIFIED phone number returns the error below instead. `email`/`dateOfBirth`/`gender` are optional. `gender` is an int: ' + enumDoc('CustomerGender') + '. NOT verified live yet (host wasn\'t running when this was generated) — shapes reflect the actual controller/DTO code, not curl output.',
      body: { phoneNumber: '+966501112222', firstName: 'Layla', lastName: 'Haddad', email: 'layla@example.com', dateOfBirth: '1995-04-12T00:00:00Z', gender: enumVal('CustomerGender', 'Female'), password: 'P@ssw0rd!2026' },
      success: { status: 'No Content', code: 204, body: null },
      includeValidation: true,
      validationFields: [
        { member: 'phoneNumber', message: "'phoneNumber' is required." },
        { member: 'firstName', message: "'firstName' is required." },
        { member: 'lastName', message: "'lastName' is required." },
        { member: 'password', message: "The field password must be a string with a minimum length of '8'." },
      ],
      errors: [{ name: '403 Forbidden - Phone number already registered', status: 'Forbidden', code: 403, body: businessRuleBody('This phone number is already registered. Please log in instead.') }],
    }),
    item({
      name: 'Register (ABP self-registration — DISABLED, reference only)',
      method: 'POST', pathSegments: ['account', 'register'], auth: 'noauth',
      description: 'Kept for reference only — do not use. This stock ABP endpoint is now switched OFF platform-wide (`Abp.Account.IsSelfRegistrationEnabled = false`, set on every Host/DbMigrator startup by `src/Eksabli.Application/Data/Seeders/SelfRegistrationDisabledDataSeederContributor.cs`). It used to create a bare `IdentityUser` with no `CustomerProfile` at all — exactly what caused customers to show up as "Unnamed" everywhere in the Business/Admin portals, since phone/OTP (via **Register Customer** above) is this app\'s real identity path and this endpoint never participated in it. Not re-verified live since being disabled — the rejection body below is inferred from ABP\'s own setting check, not curl-observed.',
      body: { userName: 'sara.customer', emailAddress: 'sara.customer@example.com', password: 'P@ssw0rd!2026', appName: 'Angular' },
      success: { name: '403 Forbidden - Self-registration disabled (inferred)', status: 'Forbidden', code: 403, body: businessRuleBody('Self registration is not enabled.') },
      include401: false,
    }),
    item({
      name: 'Forgot Password - Send Reset Code',
      method: 'POST', pathSegments: ['account', 'send-password-reset-code'], auth: 'noauth',
      description: 'Verified live: 204 No Content for a real email. For an email with no matching account it returns 403 (see error example) rather than a generic 204 — this DOES leak account existence, unlike the usual "always 200" anti-enumeration pattern; worth knowing if that matters for your threat model.',
      body: { email: 'sara.customer@example.com', appName: 'Angular' },
      success: { status: 'No Content', code: 204, body: null },
      include401: false,
      includeValidation: true, validationFields: [{ member: 'email', message: "'email' is not a valid email address." }],
      errors: [{ name: '403 Forbidden - No account with this email', status: 'Forbidden', code: 403, body: businessRuleBody('Can not find the given email address: nobody@example.com') }],
    }),
    item({
      name: 'Verify Password Reset Token',
      method: 'POST', pathSegments: ['account', 'verify-password-reset-token'], auth: 'noauth',
      description: 'Returns a plain boolean, not a DTO.',
      body: { userId: IDS.customer, resetToken: 'CfDJ8N...opaque-reset-token' },
      success: { status: 'OK', code: 200, body: true },
      include401: false,
    }),
    item({
      name: 'Reset Password',
      method: 'POST', pathSegments: ['account', 'reset-password'], auth: 'noauth',
      body: { userId: IDS.customer, resetToken: 'CfDJ8N...opaque-reset-token', password: 'N3wP@ssw0rd!2026' },
      success: { status: 'No Content', code: 204, body: null },
      include401: false,
      errors: [{ name: '404 Not Found - Bad userId', status: 'Not Found', code: 404, body: notFoundBody('IdentityUser', IDS.customer) }],
    }),
    item({
      name: 'Logout (cookie session)',
      method: 'GET', pathSegments: ['account', 'logout'], auth: 'bearer',
      description: 'Ends the ABP cookie-auth session if one exists. For pure Bearer/mobile clients there is no server-side token revocation endpoint in this codebase yet — "logging out" on Flutter means discarding the stored access/refresh token locally. Verified live: 204 No Content.',
      success: { status: 'No Content', code: 204, body: null },
    }),
  ]
);

// =========================================================================================
// 2. Profile
// =========================================================================================
const profileFolder = folder('Profile', 'ABP\'s built-in `IdentityUser` profile (`/api/account/my-profile`) plus the loyalty-specific `CustomerProfile` extension (`/api/app/customer-profile/my`) and registered `Device`s. Both `[Authorize]` with no specific permission — any authenticated user acts on their own record only. All GET/PUT/POST shapes below verified live against the running host.', [
  item({
    name: 'Get My Account Profile',
    method: 'GET', pathSegments: ['account', 'my-profile'], auth: 'bearer',
    success: { status: 'OK', code: 200, body: { userName: 'sara.customer', email: 'sara.customer@example.com', name: null, surname: null, phoneNumber: null, isExternal: false, hasPassword: true, concurrencyStamp: 'a1b2c3d4e5f6' } },
  }),
  item({
    name: 'Update My Account Profile',
    method: 'PUT', pathSegments: ['account', 'my-profile'], auth: 'bearer',
    description: 'This is where `phoneNumber` actually gets set (not at Register). `concurrencyStamp` is optional — verified live: passing `null` skips the optimistic-concurrency check entirely and always succeeds; passing a stale/made-up value (instead of echoing back the real one from Get My Account Profile) fails with a 403 "Optimistic concurrency check has been failed" error. Use `null` here unless you specifically want the safety check.',
    body: { userName: 'sara.customer', email: 'sara.customer@example.com', name: 'Sara', surname: 'Al-Amri', phoneNumber: '+966501112222', concurrencyStamp: null },
    success: { status: 'OK', code: 200, body: { userName: 'sara.customer', email: 'sara.customer@example.com', name: 'Sara', surname: 'Al-Amri', phoneNumber: '+966501112222', isExternal: false, hasPassword: true, concurrencyStamp: 'b2c3d4e5f6a1' } },
    includeValidation: true, validationFields: [{ member: 'email', message: "'email' is not a valid email address." }],
    errors: [{ name: '403 Forbidden - Stale concurrencyStamp (verified live)', status: 'Forbidden', code: 403, body: businessRuleBody("Optimistic concurrency check has been failed. The entity you're working on has modified by another user. Please discard your changes and try again.", {}) }],
  }),
  item({
    name: 'Change My Password',
    method: 'POST', pathSegments: ['account', 'my-profile', 'change-password'], auth: 'bearer',
    description: 'Verified live: a wrong `currentPassword` returns **403** (not 400) with `message: "Incorrect password."` — this app maps unclassified business-rule violations to 403, reserving 400 strictly for input-shape validation failures.',
    body: { currentPassword: 'P@ssw0rd!2026', newPassword: 'N3wP@ssw0rd!2026' },
    success: { status: 'No Content', code: 204, body: null },
    includeValidation: true, validationFields: [{ member: 'newPassword', message: 'Passwords must be at least 6 characters, contain a digit and an uppercase letter.' }],
    errors: [{ name: '403 Forbidden - Wrong current password (verified live)', status: 'Forbidden', code: 403, body: businessRuleBody('Incorrect password.') }],
  }),
  item({
    name: 'Get My Customer Profile',
    method: 'GET', pathSegments: ['app', 'customer-profile', 'my'], auth: 'bearer',
    description: 'If the account came from **Register Customer**, this is already filled in with what was submitted there. Verified live (against the older, pre-Register-Customer flow): for an account that only ever went through plain OTP login with no prior registration call, `CustomerProfile` is auto-created blank — `firstName`/`lastName` null, `gender` defaulting to 0 (Unspecified) — and this endpoint self-heals a missing row either way if one somehow still doesn\'t exist.',
    success: { status: 'OK', code: 200, body: { userId: IDS.customer, firstName: null, lastName: null, dateOfBirth: null, gender: enumVal('CustomerGender', 'Unspecified') } },
  }),
  item({
    name: 'Update My Customer Profile',
    method: 'PUT', pathSegments: ['app', 'customer-profile', 'my'], auth: 'bearer',
    description: `\`gender\` is an int: ${enumDoc('CustomerGender')}. Verified live — response includes \`id\` (a distinct CustomerProfile entity id, not the same as \`userId\`) plus audit fields.`,
    body: { firstName: 'Sara', lastName: 'Al-Amri', dateOfBirth: '1996-04-12T00:00:00Z', gender: enumVal('CustomerGender', 'Female') },
    success: { status: 'OK', code: 200, body: { id: '3a22db37-38fa-02bd-a8be-7c8740b92c28', userId: IDS.customer, firstName: 'Sara', lastName: 'Al-Amri', dateOfBirth: '1996-04-12T00:00:00Z', gender: enumVal('CustomerGender', 'Female') } },
    includeValidation: true, validationFields: [{ member: 'firstName', message: "'firstName' must not be longer than 64 characters." }],
  }),
  item({
    name: 'Register Device',
    method: 'POST', pathSegments: ['app', 'devices'], auth: 'bearer',
    description: `\`platform\` is an int: ${enumDoc('DevicePlatform')}. Push token is what Notification dispatch (business-side) targets. Verified live: returns **200** (not 201) with the full DeviceDto + audit fields.`,
    body: { platform: enumVal('DevicePlatform', 'iOS'), pushToken: 'fcm-push-token-abc123', appVersion: '1.4.2' },
    success: { status: 'OK', code: 200, body: { id: IDS.device, customerId: IDS.customer, platform: enumVal('DevicePlatform', 'iOS'), pushToken: 'fcm-push-token-abc123', lastActiveAt: '2025-11-08T09:00:00Z', appVersion: '1.4.2' } },
    includeValidation: true, validationFields: [{ member: 'pushToken', message: "'pushToken' is required." }],
  }),
  item({
    name: 'List My Devices',
    method: 'GET', pathSegments: ['app', 'devices'], auth: 'bearer',
    description: 'Plain array (no paging). Verified live (returns `[]` for a fresh user with no devices).',
    success: { status: 'OK', code: 200, body: [{ id: IDS.device, customerId: IDS.customer, platform: enumVal('DevicePlatform', 'iOS'), pushToken: 'fcm-push-token-abc123', lastActiveAt: '2025-11-08T09:00:00Z', appVersion: '1.4.2' }] },
  }),
  item({
    name: 'Remove Device (log out this device)',
    method: 'DELETE', pathSegments: ['app', 'devices', ':id'],
    opts: { pathVars: [pathVar('id', IDS.device, 'Device id')] },
    auth: 'bearer',
    description: 'In-code ownership check throws if the device does not belong to the caller (not tested live to avoid needing a second account, but the pattern matches every other ownership check in this API — see Wallet/Support folders, which WERE verified).',
    success: { status: 'OK', code: 200, body: null },
    errors: [{ name: '403 Forbidden - Not your device', status: 'Forbidden', code: 403, body: businessRuleBody('You are not authorized to remove this device.') }],
    includeNotFound: true, notFoundEntity: 'Device', notFoundIdExpr: IDS.device,
  }),
]);

// =========================================================================================
// 3. Memberships
// =========================================================================================
const membershipsFolder = folder('Memberships', '`[Authorize]` (authenticated only, no specific permission) — a customer only ever sees/creates their own `Membership` rows, enforced by scoping on `CustomerId` in the app service, not by an ABP permission. Verified live.', [
  item({
    name: 'Join Business',
    method: 'POST', pathSegments: ['app', 'memberships', 'join'], auth: 'bearer',
    description: `\`tenantId\` — ${PLACEHOLDER_NOTE} Get a real one from Admin API > Businesses (or from wherever your own signup/testing created a tenant). Verified live: returns 200 with a full \`MembershipDto\`.`,
    body: { tenantId: '{{businessId}}', referralCode: null },
    success: { status: 'OK', code: 200, body: { id: IDS.membership, customerId: IDS.customer, tenantId: '{{businessId}}', joinedAt: '2025-11-08T10:00:00Z', status: enumVal('MembershipStatus', 'Active') } },
    includeValidation: true, validationFields: [{ member: 'tenantId', message: "'tenantId' is required." }],
    errors: [{ name: '403 Forbidden - Already a member (inferred, not individually verified)', status: 'Forbidden', code: 403, body: businessRuleBody('You are already a member of this business.') }],
  }),
  item({
    name: 'List My Memberships',
    method: 'GET', pathSegments: ['app', 'memberships', 'my'], auth: 'bearer',
    description: `\`status\` is an int: ${enumDoc('MembershipStatus')}. Response is a plain array, not a \`PagedResultDto\` (no paging params on this endpoint). Verified live.`,
    success: { status: 'OK', code: 200, body: [{ id: IDS.membership, customerId: IDS.customer, tenantId: IDS.tenantStarbucks, joinedAt: '2025-11-02T09:15:00Z', status: enumVal('MembershipStatus', 'Active') }] },
  }),
]);

// =========================================================================================
// 4. Wallet
// =========================================================================================
const walletFolder = folder('Wallet', 'Cross-business wallet reads (`/api/app/memberships/my/*`) plus the per-business ledger (`/api/app/wallet/{tenantId}/transactions`). All `[Authorize]`, scoped by the caller\'s own `CustomerId`/memberships in code. Verified live.', [
  item({
    name: 'List My Wallets',
    method: 'GET', pathSegments: ['app', 'memberships', 'my', 'wallets'], auth: 'bearer',
    description: 'Plain array (no paging) — one `PointsWalletDto` per business you\'re a member of.',
    success: { status: 'OK', code: 200, body: [{ id: '5eafc433-0000-4000-8000-000000000401', membershipId: IDS.membership, tenantId: IDS.tenantStarbucks, balance: 1280, lifetimeEarned: 6420, lifetimeRedeemed: 5140, currentTierId: '70c1e655-0000-4000-8000-000000000601', currentTierName: 'Gold' }] },
  }),
  item({
    name: 'Get My Wallet QR Token',
    method: 'POST', pathSegments: ['app', 'memberships', 'my', 'wallet-qr-token'], auth: 'bearer',
    description: 'Mints a short-lived, single-use token identifying the customer at POS — scan this at checkout instead of a phone-number lookup. Same "cache-backed short-lived token" pattern used for Excel downloads and coupon redemption elsewhere in this API. Verified live: real TTL is **90 seconds**, not a round number like 60/120.',
    success: { status: 'OK', code: 200, body: { token: '3a22db390efb5e53c01c8babcfc03727', expiresInSeconds: 90 } },
  }),
  item({
    name: 'My Transaction History (per business)',
    method: 'GET', pathSegments: ['app', 'wallet', ':tenantId', 'transactions'],
    opts: { pathVars: [pathVar('tenantId', '{{businessId}}', 'Business (tenant) id — you must have a Membership here')], queries: [query('Sorting', 'creationTime desc', '', true), query('SkipCount', 0), query('MaxResultCount', 20)] },
    auth: 'bearer',
    description: `In-code check throws if you\'re not a member of this tenant (not just an ABP permission check). \`type\`/\`source\` are ints: type ${enumDoc('PointsTransactionType')}; source ${enumDoc('PointsTransactionSource')}.`,
    success: {
      status: 'OK', code: 200,
      body: paged([{ id: IDS.transaction, walletId: '5eafc433-0000-4000-8000-000000000401', type: enumVal('PointsTransactionType', 'Earn'), points: 120, source: enumVal('PointsTransactionSource', 'Purchase'), referenceId: null, expiresAt: '2026-11-03T00:00:00Z', createdByEmployeeId: null, reason: null, tierMultiplierSnapshot: 1.5, creationTime: '2025-11-03T14:22:10Z' }], 214),
    },
    errors: [{ name: '403 Forbidden - Not a member of this business', status: 'Forbidden', code: 403, body: businessRuleBody('You do not have a membership with this business.') }],
  }),
]);

// =========================================================================================
// 5. Rewards (catalog browse)
// =========================================================================================
const rewardsFolder = folder('Rewards', 'Read-only catalog browse lives on the `Coupon` controller (`/api/app/coupon/catalog/{tenantId}`), not `/api/app/reward` — that CRUD surface is business-only (see the Business API collection). `[Authorize]`, authenticated only. Verified live (returns an empty page for a tenantId with no rewards, rather than a 404 — no tenant-existence check here either).', [
  item({
    name: 'Browse Reward Catalog',
    method: 'GET', pathSegments: ['app', 'coupon', 'catalog', ':tenantId'],
    opts: { pathVars: [pathVar('tenantId', '{{businessId}}', 'Business (tenant) id')], queries: [query('Sorting', 'pointsCost asc', '', true), query('SkipCount', 0), query('MaxResultCount', 20)] },
    auth: 'bearer',
    description: `\`type\` is an int: ${enumDoc('RewardType')}.`,
    success: {
      status: 'OK', code: 200,
      body: paged([{ id: '{{rewardId}}', tenantId: '{{businessId}}', nameAr: 'مشروب غراندي مجاني', nameEn: 'Free Grande Beverage', type: enumVal('RewardType', 'FreeProduct'), pointsCost: 450, stockRemaining: 37, validFrom: '2025-10-01T00:00:00Z', validTo: '2026-03-31T23:59:59Z', imageBlobName: 'rewards/grande-beverage.png', approvalThresholdPoints: null }]),
    },
  }),
]);

// =========================================================================================
// 6. Coupons
// =========================================================================================
const couponsFolder = folder('Coupons', '`/api/app/coupon/*` — redeeming debits points and mints a `Coupon`; `my` lists everything you\'ve redeemed. `[Authorize]`, authenticated only. `my` verified live (returns `[]`).', [
  item({
    name: 'Redeem Reward',
    method: 'POST', pathSegments: ['app', 'coupon', 'redeem'], auth: 'bearer',
    body: { tenantId: '{{businessId}}', rewardId: '{{rewardId}}' },
    success: { status: 'OK', code: 200, body: { id: IDS.coupon, rewardId: '{{rewardId}}', rewardNameAr: 'مشروب غراندي مجاني', rewardNameEn: 'Free Grande Beverage', membershipId: IDS.membership, tenantId: '{{businessId}}', code: 'EKS-9F3K-7QRT', status: enumVal('CouponStatus', 'Issued'), issuedAt: '2025-11-08T10:00:00Z', redeemedAt: null, redeemedByEmployeeId: null, redeemedBranchId: null } },
    includeValidation: true, validationFields: [{ member: 'rewardId', message: "'rewardId' is required." }],
    errors: [
      { name: '403 Forbidden - Insufficient points (inferred from confirmed 403-for-business-rules pattern)', status: 'Forbidden', code: 403, body: businessRuleBody('You do not have enough points to redeem this reward.') },
      { name: '403 Forbidden - Out of stock (inferred)', status: 'Forbidden', code: 403, body: businessRuleBody('This reward is out of stock.') },
    ],
    includeNotFound: true, notFoundEntity: 'Reward', notFoundIdExpr: '{{rewardId}}',
  }),
  item({
    name: 'List My Coupons',
    method: 'GET', pathSegments: ['app', 'coupon', 'my'],
    opts: { queries: [query('tenantId', '{{businessId}}', 'Optional — omit for coupons across every business', true)] },
    auth: 'bearer',
    description: `Plain array (no paging). \`status\` is an int: ${enumDoc('CouponStatus')}. Verified live.`,
    success: { status: 'OK', code: 200, body: [{ id: IDS.coupon, rewardId: '{{rewardId}}', rewardNameAr: 'مشروب غراندي مجاني', rewardNameEn: 'Free Grande Beverage', membershipId: IDS.membership, tenantId: '{{businessId}}', code: 'EKS-9F3K-7QRT', status: enumVal('CouponStatus', 'Issued'), issuedAt: '2025-11-08T10:00:00Z', redeemedAt: null, redeemedByEmployeeId: null, redeemedBranchId: null }] },
  }),
]);

// =========================================================================================
// 7. Categories (read-only — closest thing to "discovery" that's actually implemented)
// =========================================================================================
const categoriesFolder = folder('Categories', '`[AllowAnonymous]` reads — business category taxonomy. There is currently no store discovery/search/nearby endpoint at all in this API (only `follow`/`business/profile`, both scoped to a specific known `tenantId`), so Categories is the only public "browse" surface today; a future discovery endpoint would likely filter by this. List verified live (200, real data from the seeded DB); detail-by-bad-id verified live (404).', [
  item({
    name: 'List Categories',
    method: 'GET', pathSegments: ['app', 'category'],
    opts: { queries: [query('ParentCategoryId', '', 'Filter to subcategories of a parent', true), query('FilterText', 'cafe', '', true), query('SkipCount', 0), query('MaxResultCount', 20)] },
    auth: 'noauth',
    success: { status: 'OK', code: 200, body: paged([{ id: IDS.category, nameAr: 'مقهى', nameEn: 'Cafe', iconBlobName: 'categories/cafe.png', parentCategoryId: null }]) },
  }),
  item({
    name: 'Get Category Detail',
    method: 'GET', pathSegments: ['app', 'category', ':id'],
    opts: { pathVars: [pathVar('id', IDS.category, 'Category id')] },
    auth: 'noauth',
    success: { status: 'OK', code: 200, body: { id: IDS.category, nameAr: 'مقهى', nameEn: 'Cafe', iconBlobName: 'categories/cafe.png', parentCategoryId: null } },
    include401: false,
    includeNotFound: true, notFoundEntity: 'Category', notFoundIdExpr: IDS.category,
  }),
]);

// =========================================================================================
// 8. Favorites (Follow)
// =========================================================================================
const favoritesFolder = folder('Favorites', '`/api/app/follow/*` — `FollowAsync`/`UnfollowAsync`/`GetMyFollowsAsync` are all `[Authorize]` with no specific permission (any authenticated customer, own rows only). The business-side "who follows us" list (`GET follow/followers`, gated by `Eksabli.Followers.View`) lives in the Business API collection instead — same `Follow` entity, different read. Follow/List My Follows verified live (204 and `[]` respectively — no tenant-existence check on Follow either).', [
  item({
    name: 'Follow Business',
    method: 'POST', pathSegments: ['app', 'follow', ':tenantId'],
    opts: { pathVars: [pathVar('tenantId', '{{businessId}}', 'Business (tenant) id')] },
    auth: 'bearer',
    description: 'Verified live: 204 No Content, no request body.',
    success: { status: 'No Content', code: 204, body: null },
  }),
  item({
    name: 'Unfollow Business',
    method: 'DELETE', pathSegments: ['app', 'follow', ':tenantId'],
    opts: { pathVars: [pathVar('tenantId', '{{businessId}}', 'Business (tenant) id')] },
    auth: 'bearer',
    success: { status: 'No Content', code: 204, body: null },
  }),
  item({
    name: 'List My Follows',
    method: 'GET', pathSegments: ['app', 'follow', 'my'], auth: 'bearer',
    description: 'Plain array (no paging). Verified live.',
    success: { status: 'OK', code: 200, body: [{ id: '11111111-2222-4000-8000-000000000001', customerId: IDS.customer, tenantId: IDS.tenantStarbucks, followedAt: '2025-09-12T10:00:00Z' }] },
  }),
]);

// =========================================================================================
// 9. Referrals
// =========================================================================================
const referralsFolder = folder('Referrals', '`/api/app/referral/*`, `[Authorize]`, authenticated only. `my` verified live (`[]`).', [
  item({
    name: 'Get My Referral Code',
    method: 'GET', pathSegments: ['app', 'referral', 'my-code'],
    opts: { queries: [query('tenantId', '{{businessId}}', 'Business to generate a referral code for')] },
    auth: 'bearer',
    description: '`code` is itself a uuid (used as the referral token, e.g. embedded in a share link/QR) — not a `MembershipDto`.',
    success: { status: 'OK', code: 200, body: { code: 'd6274cbb-0000-4000-8000-000000001201' } },
  }),
  item({
    name: 'List My Referrals',
    method: 'GET', pathSegments: ['app', 'referral', 'my'], auth: 'bearer',
    description: `Plain array (no paging). \`status\` is an int: ${enumDoc('ReferralStatus')}.`,
    success: { status: 'OK', code: 200, body: [{ id: 'd6274cbb-0000-4000-8000-000000001201', referrerMembershipId: IDS.membership, refereeCustomerId: '22222222-3333-4000-8000-000000000001', tenantId: IDS.tenantStarbucks, status: enumVal('ReferralStatus', 'Completed') }] },
  }),
]);

// =========================================================================================
// 10. Achievements
// =========================================================================================
const achievementsFolder = folder('Achievements', '`AchievementsController` is class-level `[Authorize(Eksabli.Achievements)]` — the SAME "Default" permission staff use to manage badges. **Verified live: a plain authenticated customer gets a 403 with an EMPTY body** (this is a policy-attribute rejection, not a thrown business exception, so it does not get the JSON error wrapper). There is no separate, permission-free "browse badges" read for ordinary customers today — likely worth raising with whoever owns the permission grants if badges are meant to be customer-visible by default, rather than working around it client-side.', [
  item({
    name: 'List Achievement Definitions',
    method: 'GET', pathSegments: ['app', 'achievement'],
    opts: { queries: [query('SkipCount', 0), query('MaxResultCount', 20)] },
    auth: 'bearer',
    include403: true, permission: 'Eksabli.Achievements (Default) — confirmed live: an ordinary customer 403s here',
    success: { status: 'OK', code: 200, body: paged([{ id: IDS.achievement, tenantId: IDS.tenantStarbucks, name: 'Coffee Enthusiast', criteriaJson: '{"visits":10,"withinDays":30}' }]) },
  }),
  item({
    name: 'Get My Awards for a Membership',
    method: 'GET', pathSegments: ['app', 'achievement', 'membership', ':membershipId', 'awards'],
    opts: { pathVars: [pathVar('membershipId', IDS.membership, 'Membership id')] },
    auth: 'bearer',
    include403: true, permission: 'Eksabli.Achievements (Default) — see folder note',
    description: 'Plain array (no paging).',
    success: { status: 'OK', code: 200, body: [{ id: '33333333-4444-4000-8000-000000000001', membershipId: IDS.membership, achievementId: IDS.achievement, awardedAt: '2025-10-15T00:00:00Z' }] },
  }),
]);

// =========================================================================================
// 11. Support
// =========================================================================================
const supportFolder = folder('Support', '`/api/app/support-ticket/*`, `[Authorize]` for create/get/reply (own ticket only, enforced by `EnsureCanAccessAsync` in code). Note: `GetListAsync` (the full queue) requires `Eksabli.SupportTickets.Manage` and is NOT available to ordinary customers — there is currently no "list all my tickets" endpoint, only get-by-id for a ticket you already know the id of. See the Admin API collection for the queue view.', [
  item({
    name: 'Create Support Ticket',
    method: 'POST', pathSegments: ['app', 'support-ticket'], auth: 'bearer',
    description: `\`priority\` is an int: ${enumDoc('SupportTicketPriority')}.`,
    body: { subject: 'Points missing from last purchase', body: 'I made a purchase yesterday but only got half the points I expected.', priority: enumVal('SupportTicketPriority', 'Medium') },
    success: { status: 'OK', code: 200, body: { id: IDS.ticket, tenantId: null, customerId: IDS.customer, subject: 'Points missing from last purchase', status: enumVal('SupportTicketStatus', 'Open'), priority: enumVal('SupportTicketPriority', 'Medium'), messages: [] } },
    includeValidation: true, validationFields: [{ member: 'subject', message: "'subject' is required." }],
  }),
  item({
    name: 'Get My Support Ticket',
    method: 'GET', pathSegments: ['app', 'support-ticket', ':id'],
    opts: { pathVars: [pathVar('id', IDS.ticket, 'SupportTicket id')] },
    auth: 'bearer',
    success: {
      status: 'OK', code: 200,
      body: { id: IDS.ticket, tenantId: null, customerId: IDS.customer, subject: 'Points missing from last purchase', status: enumVal('SupportTicketStatus', 'Open'), priority: enumVal('SupportTicketPriority', 'Medium'), messages: [{ id: '44444444-5555-4000-8000-000000000001', ticketId: IDS.ticket, senderId: IDS.customer, body: 'I made a purchase yesterday but only got half the points I expected.', createdAt: '2025-11-06T16:40:00Z' }] },
    },
    errors: [{ name: '403 Forbidden - Not your ticket', status: 'Forbidden', code: 403, body: businessRuleBody('You are not authorized to view this support ticket.') }],
    includeNotFound: true, notFoundEntity: 'SupportTicket', notFoundIdExpr: IDS.ticket,
  }),
  item({
    name: 'Reply to Support Ticket',
    method: 'POST', pathSegments: ['app', 'support-ticket', ':id', 'messages'],
    opts: { pathVars: [pathVar('id', IDS.ticket, 'SupportTicket id')] },
    auth: 'bearer',
    body: { body: "Following up - still hasn't been resolved." },
    success: { status: 'OK', code: 200, body: { id: '55555555-6666-4000-8000-000000000001', ticketId: IDS.ticket, senderId: IDS.customer, body: "Following up - still hasn't been resolved.", createdAt: '2025-11-08T11:00:00Z' } },
    errors: [{ name: '403 Forbidden - Not your ticket', status: 'Forbidden', code: 403, body: businessRuleBody('You are not authorized to reply to this support ticket.') }],
    includeNotFound: true, notFoundEntity: 'SupportTicket', notFoundIdExpr: IDS.ticket,
  }),
]);

// =========================================================================================
// Collection assembly
// =========================================================================================
const collection = {
  info: {
    name: 'Eksabli Mobile API',
    description:
      'Customer-facing (Host-realm) API, rebuilt directly from the live Swagger doc of `Eksabli.HttpApi.Host`, the real `[Authorize]` attributes in `src/Eksabli.HttpApi/Controllers/` (ABP\'s auto-API-controllers are disabled in this repo — every app service has a hand-written controller instead), and live curl verification with a throwaway registered user against `https://localhost:44330`.\n\n' +
      '**Base URLs** — two, because token issuance is not an ABP application service: `{{baseUrl}}` = `https://localhost:44330/api` for everything else, `{{issuerUrl}}` = `https://localhost:44330` for `/connect/token`.\n\n' +
      '**Auth flow**: for a new customer, run **Register Customer -> Login (OTP grant)** (the register call sends the code itself, no separate Send OTP needed). For a returning customer, either **Send OTP -> Login (OTP grant)**, or **Login (password grant)** once they have a confirmed phone number. Every grant\'s Tests script saves `accessToken`/`refreshToken` automatically (note: OAuth2 responses use `access_token`/`refresh_token`, snake_case). A collection-level Pre-request script auto-refreshes when the token is close to expiry. Note: stock ABP self-registration (`/api/account/register`) is disabled platform-wide as of this revision — see that folder entry\'s own description for why; it is kept only as a labeled reference, not a working flow.\n\n' +
      '**Two distinct error shapes — do not confuse them:**\n' +
      '- Missing/invalid Bearer token (401), or a permission-attribute rejection like `[Authorize(SomePermission)]` (403) -> **empty body**, verified live on `/api/app/achievement` and `/api/app/category` (POST).\n' +
      '- A business rule thrown from inside app-service code (wrong password, entity not found, ownership check) -> ABP\'s JSON `{ error: {...} }` wrapper. Confirmed live: **wrong password change and "email not found" both return 403** (not 400) with `data: null`; entity-not-found (bad id) returns **404** with message `"There is no entity {Type} with id = {id}!"`. Only real input-shape validation failures (missing required fields, bad email format) return 400, with message `"Your request is not valid!"`.\n' +
      '- `/connect/token` errors follow plain OAuth2 `{ error, error_description }` instead of either ABP shape.\n\n' +
      '**Known gaps vs. the original design docs — intentionally NOT invented as fake endpoints:**\n' +
      '- No store discovery/search/nearby/list endpoint exists yet (only `follow/{tenantId}`, `business/profile`, both need an already-known `tenantId`). Categories is the only public browse surface today.\n' +
      '- No customer notification inbox: `/api/app/notification` GET is the tenant\'s *sent* log, gated by `Eksabli.Notifications.Send` (a marketing-staff permission) — not readable by ordinary customers.\n' +
      '- No customer-facing Campaigns/Offers read: both controllers require `Eksabli.Campaigns`/`Eksabli.Offers` (Default), which are staff-oriented permissions, not `[AllowAnonymous]`/open-to-customer.\n' +
      '- No Leaderboard and no app-level Settings (language/theme) endpoint exist at all.\n' +
      '- Achievements browsing is gated behind the same staff "Default" permission used for badge CRUD (verified live, see folder note).\n' +
      '- Support ticket "list all my tickets" doesn\'t exist — only get-by-id once you already have one.\n' +
      '- Several write endpoints (Join Business, Follow, Redeem catalog browse) do not validate that the referenced `tenantId` actually exists — verified live with an all-zero GUID, which was silently accepted.',
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
          '    const issuerUrl = pm.collectionVariables.get("issuerUrl") || pm.environment.get("issuerUrl");',
          '    if (refreshToken && issuerUrl) {',
          '        pm.sendRequest({',
          '            url: issuerUrl + "/connect/token",',
          '            method: "POST",',
          '            header: { "Content-Type": "application/x-www-form-urlencoded" },',
          '            body: { mode: "urlencoded", urlencoded: [',
          '                { key: "grant_type", value: "refresh_token" },',
          '                { key: "refresh_token", value: refreshToken },',
          '                { key: "client_id", value: "Eksabli_App" }',
          '            ] }',
          '        }, function (err, res) {',
          '            if (!err && res.code === 200) {',
          '                const json = res.json();',
          '                pm.collectionVariables.set("accessToken", json.access_token);',
          '                if (json.refresh_token) { pm.collectionVariables.set("refreshToken", json.refresh_token); }',
          '                pm.collectionVariables.set("tokenExpiresAt", Date.now() + (json.expires_in * 1000));',
          '            }',
          '        });',
          '    }',
          '}',
        ],
      },
    },
  ],
  variable: [
    { key: 'baseUrl', value: 'https://localhost:44330/api', type: 'string' },
    { key: 'issuerUrl', value: 'https://localhost:44330', type: 'string' },
    { key: 'accessToken', value: '', type: 'string' },
    { key: 'refreshToken', value: '', type: 'string' },
    { key: 'tokenExpiresAt', value: '0', type: 'string' },
    { key: 'businessId', value: IDS.tenantStarbucks, type: 'string' },
    { key: 'branchId', value: '3c8da211-0000-4000-8000-000000000201', type: 'string' },
    { key: 'userId', value: IDS.customer, type: 'string' },
    { key: 'rewardId', value: IDS.reward, type: 'string' },
    { key: 'campaignId', value: 'a3f41988-0000-4000-8000-000000000901', type: 'string' },
    { key: 'transactionId', value: IDS.transaction, type: 'string' },
  ],
  item: [
    authFolder, profileFolder, membershipsFolder, walletFolder, rewardsFolder, couponsFolder,
    categoriesFolder, favoritesFolder, referralsFolder, achievementsFolder, supportFolder,
  ],
};

const outPath = path.join(__dirname, '..', 'Eksabli-Mobile-API.postman_collection.json');
fs.writeFileSync(outPath, JSON.stringify(collection, null, 2));
console.log('Wrote', outPath);
let total = 0;
for (const f of collection.item) total += f.item.length;
console.log('Folders:', collection.item.length, 'Requests:', total);
