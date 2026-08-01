# 6. Dashboards & Admin

[← Back to index](README.md)

Both surfaces below live in **one Angular application** — the same one already scaffolded in
`angular/` — split by ABP's Host (platform admin) vs Tenant (business) realms, per the
[architecture decision](02-system-architecture.md#high-level-architecture). This document describes
them separately because they serve different users, not because they're different codebases.

## Contents
- [6. Business dashboard (Tenant realm)](#6-business-dashboard-tenant-realm)
- [7. Admin panel (Host realm)](#7-admin-panel-host-realm)
- [12. Reports & analytics](#12-reports--analytics)

## 6. Business dashboard (Tenant realm)

| Module | Purpose | Key elements | Primary role |
|---|---|---|---|
| Dashboard (home) | At-a-glance business health | Active members, points issued/redeemed this period, active campaigns, low-stock reward alerts | Owner, Manager |
| Analytics | Deeper self-serve exploration | Member growth curve, redemption rate, per-branch comparison, tier distribution | Owner, Manager |
| Customer List | Every member of this business | Searchable/filterable table (tier, join date, last activity, balance), drill into `CustomerDetail` | Owner, Manager, Marketing |
| Customer Detail | One member's full picture *within this business* (never cross-business — that would break the [identity realm boundary](02-system-architecture.md#two-identity-realms-the-key-decision)) | Balance, tier, transaction history, manual point adjustment (audited), coupons redeemed | Owner, Manager |
| Campaigns | Create/manage promotions | List with status (Draft/Active/Ended), campaign builder (type, rules, segment, schedule), target-segment preview before activating | Owner, Marketing |
| Offers | Manage displayed deals (distinct from point-redeemable Rewards — see [database design](03-database-design.md#campaigns--notifications)) | List, create/edit, branch scoping, schedule | Owner, Marketing |
| Rewards | Manage the redemption catalog | List, create/edit (cost, stock, validity), redemption stats per reward | Owner, Manager |
| Point Rules | Configure how points are earned | Per-currency-unit rate, per-visit flat rate, tier multipliers, expiration policy | Owner |
| Coupons | Audit trail of issued/redeemed rewards | Filterable table (status, branch, staff member who redeemed) — the operational view that catches redemption-fraud patterns | Owner, Manager |
| Branches | Manage locations | List, create/edit (address, geo, hours), QR code generator per branch | Owner |
| Employees | Staff management | Invite staff, assign role + branch(es), deactivate access | Owner |
| Transactions | Raw points ledger view | Filterable by type/date/branch/staff — the "show your work" screen behind Analytics' summarized charts | Owner, Manager |
| Notifications | Send + review outbound messages | Compose (manual or campaign-linked), delivery log, channel (push/email/SMS) with quota usage against plan | Owner, Marketing |
| Followers | Customers following but not yet members ([`Follow` entity](03-database-design.md#engagement--gamification)) | List, "convert to campaign target" action | Marketing |
| Reports | Exportable reports | See [Reports & Analytics](#12-reports--analytics) below | Owner |
| Subscription & Billing | Plan management | Current plan + usage against quota (branches, SMS credits, active members), upgrade/downgrade, invoice history | Owner |
| Settings | Business configuration | Profile/logo/branding, notification sender identity, integrations, danger zone (cancel subscription) | Owner |

**POS mode** (cashier role, described in the [business journey](04-product-experience.md#business-tenant-staff-journey))
is a deliberately reduced view of this same app — award points, redeem coupons, nothing else —
rather than a separate build.

## 7. Admin panel (Host realm)

| Module | Purpose | Key elements |
|---|---|---|
| Tenant Management | Oversee all businesses on the platform | List (status, plan, signup date), approve/suspend, view-as-tenant (read-mostly, fully audited — never silent) |
| User Management | Platform-level user oversight | Search customers/staff across tenants (Host-level only — this is exactly the kind of cross-tenant query that's legitimate for a Super Admin and illegitimate for a Business Owner, enforced by the same permission system throughout) |
| Subscriptions & Payments | Billing oversight | Plan catalog management, tenant subscription status, payment/invoice reconciliation, manual refunds (Billing Admin role) |
| Platform Campaigns | Platform-wide announcements (not tenant campaigns) | E.g. "New feature" banners, platform-level promotions |
| Categories | Business category taxonomy | CRUD, used by discovery/search |
| Support Tickets | Customer + business support | Queue by status/priority, thread view, linked account context |
| Reports | Platform-wide reporting | See [Reports & Analytics](#12-reports--analytics) below |
| Feature Flags | Plan entitlements + rollout flags | Thin UI over ABP's existing Feature Management module — not a custom system |
| Audit Logs | Who-did-what across the platform | Thin UI over ABP's existing Audit Logging module |
| System Settings | Global configuration | Notification provider credentials, default plan, maintenance mode |

## 12. Reports & analytics

| Report type | Audience | Example metrics |
|---|---|---|
| Business reports | Business Owner | Member growth, points issued vs redeemed ratio, redemption rate by reward, branch comparison |
| Customer reports | Business Owner/Marketing | Segment breakdown (new/active/at-risk/churned), tier distribution, top customers by lifetime value |
| Financial reports | Platform Billing Admin, Business Owner (their own) | MRR/ARR, plan mix, churn rate, invoice aging |
| Marketing reports | Business Marketing | Campaign performance (sent, opened, redeemed, revenue attributed), notification delivery rates by channel |
| Platform analytics | Platform Super Admin | Tenant growth, platform-wide DAU/MAU, category mix, support ticket volume/resolution time |

**KPIs worth defining precisely before building dashboards around them** (ambiguous KPI definitions
are a recurring source of "the dashboard number doesn't match what I expected" support tickets):

| KPI | Definition to lock down |
|---|---|
| Active member | Membership with ≥1 `PointsTransaction` in the trailing N days (define N per report context — 30/60/90 all have legitimate uses, but pick one as the default and label it) |
| Redemption rate | Redeemed points ÷ earned points over a period — per business, not platform-wide (platform-wide blends businesses with very different reward economics) |
| Churn (per business) | No activity for N days *within that membership* — explicitly **not** account-level, per the [customer lifecycle](01-business-strategy.md#customer-lifecycle) point that churn is per-membership |
| MRR/ARR | Standard SaaS definition, computed from `TenantSubscription` + `Invoice`, excluding trialing tenants until their first paid invoice |

At MVP scale, these reports can run directly against the transactional Postgres database (with the
read replica introduced at the "1,000 stores" tier — see [Scalability](02-system-architecture.md#scalability)).
Don't build a separate analytics pipeline before there's reporting load that actually competes with
transactional traffic; that's explicitly a later-tier concern in this design, not a Phase 1–4 one.
