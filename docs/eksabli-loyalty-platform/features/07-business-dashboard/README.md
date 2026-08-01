# Feature 07 — Business Dashboard (Core)

[← Back to feature index](../README.md)

## Overview

The parts of the tenant-side Angular dashboard that aren't owned by a specific domain feature:
the dashboard home, analytics/reports, and business-level settings. Every *other* feature also adds
its own screens to this same dashboard (Feature 02 adds POS/Point Rules, Feature 03 adds Rewards,
Feature 05 adds Campaigns) — this feature is the shell plus the cross-cutting reporting layer that
reads across all of them.

- **MVP phase:** 1 (shell, branches/employees — actually delivered by [Feature 01](../01-identity-multi-tenancy/README.md)), 4 (analytics/reports)
- **Depends on:** all other tenant-facing features, since it aggregates their data

## Domain model

No new entities — this feature is a read/reporting layer over `Membership`, `PointsTransaction`,
`Campaign`, `Notification`, etc. from other features.

## Business rules

**Lock down KPI definitions before building charts around them** — ambiguous definitions are a
recurring source of "the dashboard number doesn't match what I expected" tickets. From
[Dashboards & Admin §12](../../06-dashboards-admin.md#12-reports--analytics):

| KPI | Definition |
|---|---|
| Active member | `Membership` with ≥1 `PointsTransaction` in the trailing N days (pick one default N, e.g. 30, and label it) |
| Redemption rate | Redeemed points ÷ earned points, **per business** (platform-wide blending distorts businesses with different reward economics) |
| Churn | No activity for N days **within that membership** — never account-level, since [churn is per-membership](../../01-business-strategy.md#customer-lifecycle) by design |
| MRR/ARR | Standard SaaS definition from [Feature 04](../04-billing-subscriptions/README.md)'s `TenantSubscription`/`Invoice`, excluding trialing tenants until first paid invoice |

**Reports run directly against the transactional Postgres database** until the read-replica tier
(see [Scalability](../../02-system-architecture.md#scalability)) — don't build a separate analytics
pipeline before there's reporting load that actually competes with transactional traffic.

## API surface

| Group | Resources | Realm |
|---|---|---|
| `/api/reports/*` | dashboards, exports | Tenant (business reports) |

## Screens

**Angular:** Dashboard (home — active members, points issued/redeemed this period, active
campaigns, low-stock reward alerts), Analytics (member growth, redemption rate, per-branch
comparison, tier distribution), Customer List/Detail (shared with Feature 02), Transactions (raw
ledger view), Reports (exportable), Settings (business profile/branding, notification sender
identity, integrations, cancel subscription).

No mockup built yet for this feature — the customer-facing screens have been prioritized for
mockups so far.

## Permissions

Owner/Manager for most views; Reports export may warrant its own permission if it's judged sensitive
enough to restrict from Marketing-role staff.

## Implementation checklist

- [ ] Dashboard home + Analytics query services (read-only, cross-entity — likely custom repository
      methods or a dedicated read-model service rather than generic `IRepository<T>` calls)
- [ ] Reports export reusing the existing MiniExcelLibs + token-gated download pattern
- [ ] Settings screens for `BusinessProfile` (from Feature 01) — branding, notification sender identity
- [ ] KPI calculation logic centralized in one place (a `ReportingAppService` or similar), not
      duplicated per chart, so the KPI table above stays the single source of truth
- [ ] Permissions for report export if warranted
- [ ] Localization keys

## Open questions

- None specific to this feature — it inherits open questions from whichever features it reports on.
