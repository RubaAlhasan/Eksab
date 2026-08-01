# Feature 05 — Campaigns & Notifications

[← Back to feature index](../README.md)

## Overview

The retention engine: time-boxed, rule-driven promotions (birthday, double points, win-back, spend-X-get-Y...)
and the multi-channel (push/email/SMS/in-app) delivery system that reaches customers about them.

- **MVP phase:** 3
- **Depends on:** [02 — Membership & Wallet](../02-membership-wallet/README.md) (targeting reads membership/activity data; multipliers hook into the points pipeline)

## Domain model

| Entity | Purpose | Key fields | Notes |
|---|---|---|---|
| `Campaign` | A time-boxed, rule-driven promotion | `TenantId`, `Name`, `Type`, `Rules (json)`, `StartDate/EndDate`, `Status` | `IMultiTenant` |
| `CampaignTargetRule` | Segment definition a campaign targets | `CampaignId`, `SegmentType` (Tier/Inactive/NewCustomer/All), `Parameters (json)` | Child of `Campaign` — no separate repository |
| `Offer` | A displayed deal, distinct from a points-cost `Reward` | `TenantId`, `BranchId`, `Title`, `Description`, `StartDate/EndDate`, `ImageBlobName` | `IMultiTenant` |
| `Notification` | Delivery record for one message (or a broadcast) | `MembershipId` (nullable), `TenantId`, `CampaignId` (nullable), `Channel`, `Title`, `Body`, `Status`, `SentAt` | `IMultiTenant`. What [Reports](../07-business-dashboard/README.md) reads for delivery stats |

Full ERD: [Database Design → Campaigns & notifications](../../03-database-design.md#campaigns--notifications).

## Business rules

**Two distinct evaluation modes — this is the part that's easy to under-build:**

| Mode | Campaign types | How it runs |
|---|---|---|
| Scheduled segment sweep | Birthday, win-back (inactive), VIP, new-customer | A daily background job evaluates the segment and enqueues notifications |
| Real-time transactional rule | Double points, spend-X-get-Y | Evaluated **inside the point-award request itself** (feature 02's pipeline), not a batch job |

A campaign engine that only implements the batch-sweep mode can *talk about* "Double Points
Weekend" but can't actually double points at the register — see
[Loyalty Engine §10](../../07-loyalty-engine.md#10-campaigns) for the full campaign-type catalog and
diagram. Both modes are required before this feature is considered done.

**Notification fan-out must be rate-limited per tenant, not just globally** — otherwise one
business's campaign can starve another's transactional notifications (a named risk in
[Business Strategy §21](../../01-business-strategy.md#21-risks)). Buy the channel providers (FCM/OneSignal,
a transactional email provider, a regional SMS aggregator); build the dispatcher, per-tenant quota
enforcement, and the `Notification` log.

## API surface

| Group | Resources | Realm |
|---|---|---|
| `/api/campaigns/*` | CRUD, activate, target-segment preview | Tenant (Marketing+) |
| `/api/notifications/*` | send, delivery log, customer channel preferences | Tenant (send) + Host (customer preferences) |

## Screens

**Flutter:** Campaigns/Offers feed, Notifications inbox, Birthday Rewards.

**Angular (business dashboard):** Campaign builder (type, rules, segment, schedule) + target-segment
preview + activate, Offers management, Notifications compose + delivery log + quota usage.

**Working mockup:** the Home screen shows two active campaigns as image-banner cards (Starbucks
"Double Points Weekend," Nike "20% Off") plus a small badge on each business's wallet-card mark
indicating it has a live campaign:
**https://claude.ai/code/artifact/1bd0c7e1-9f64-4530-b4a1-fa694accd049**

## Permissions

`Eksabli.Campaigns.Create/Edit/Activate` (Owner/Marketing), `Eksabli.Notifications.Send` (Owner/Marketing).

## Implementation checklist

- [ ] `Campaign`, `CampaignTargetRule`, `Offer`, `Notification` entities in `src/Eksabli.Domain/Campaigns/`
- [ ] Constants in `src/Eksabli.Domain.Shared/`
- [ ] EF Core config in `EksabliDbContext` (all `IMultiTenant`)
- [ ] Migration: `dotnet ef migrations add Added_CampaignsNotifications`
- [ ] Campaign evaluation: (a) scheduled background job for segment sweeps, (b) inline hook in the
      Feature 02 point-award pipeline for real-time multiplier/bonus rules
- [ ] `INotificationSender` abstraction + channel adapters (push/email/SMS), invoked only through the
      background job queue
- [ ] DTOs + `ICampaignAppService`/`INotificationAppService`
- [ ] Permissions: `Eksabli.Campaigns.*`, `Eksabli.Notifications.Send`
- [ ] Localization keys (campaign/notification copy needs the bilingual content treatment — see
      [Database Design → Cross-cutting notes](../../03-database-design.md#cross-cutting-notes))

## Open questions

- Background job provider: ABP's default in-process executor is fine to start; migrate to Hangfire
  (Postgres/Redis storage) once campaign fan-out volume matters — not a blocker for starting this
  feature, but worth deciding before Phase 3 ships broadly. See
  [System Architecture → Background jobs](../../02-system-architecture.md#background-jobs).
- Push/SMS/email provider selection not yet made — should account for the confirmed Arabic/English
  market (regional SMS aggregator support varies significantly by country).
