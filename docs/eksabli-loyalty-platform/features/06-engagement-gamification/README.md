# Feature 06 — Engagement & Gamification

[← Back to feature index](../README.md)

## Overview

Retention mechanics beyond the core loop: referrals, achievements/badges, and following a business
before becoming a full member. Deliberately **not** platform-wide defaults — several of these
mechanics fit some business categories (café, gym) and feel out of place for others (professional
services), so they're tenant-level opt-in features, not forced UI.

- **MVP phase:** 3 (referral), 4 (achievements/badges — tenant opt-in)
- **Depends on:** [02 — Membership & Wallet](../02-membership-wallet/README.md), [05 — Campaigns & Notifications](../05-campaigns-notifications/README.md) (referral completion triggers a campaign-style bonus)

## Domain model

| Entity | Purpose | Key fields | Notes |
|---|---|---|---|
| `Referral` | A customer inviting another customer into a specific business | `ReferrerMembershipId`, `RefereeCustomerId`, `TenantId`, `Status` | `IMultiTenant` |
| `Achievement` | Badge definition (platform-wide or tenant-specific) | `TenantId` (nullable), `Name`, `Criteria (json)` | |
| `AchievementAward` | Which customer earned which badge, and when | `MembershipId`, `AchievementId`, `AwardedAt` | Unique `(MembershipId, AchievementId)` |
| `Follow` | A customer following a business pre-membership | `CustomerId`, `TenantId`, `FollowedAt` | Unique `(CustomerId, TenantId)` — **deliberately serves both "favorite" and "follow" concepts**, see below |

Full ERD: [Database Design → Engagement & gamification](../../03-database-design.md#engagement--gamification).

## Business rules

**`Follow` intentionally replaces the brief's separate `Favorites`/`Followers` tables** — both mean
"a customer wants to see this business's offers without necessarily being a member yet." One table,
reused for both the customer-facing "favorite" UI concept and the business-facing "follower"
marketing-target concept, rather than two parallel tables that quietly diverge over time. See
[Database Design](../../03-database-design.md#engagement--gamification) for the full reasoning.

**Badges/streaks/spin-wheel/scratch-cards are tenant-level opt-in** (an ABP Feature Management flag,
same mechanism as [plan entitlements](../04-billing-subscriptions/README.md)), not a forced
platform-wide mechanic. Spin-wheel/scratch-card mechanics specifically carry a **real legal risk**
(chance-based promotions attached to purchases are regulated as gambling-adjacent in some
jurisdictions) — get a legal read for target markets before building, not after. Full category-fit
table: [Loyalty Engine §11](../../07-loyalty-engine.md#11-customer-engagement).

## API surface

Not yet named in the architecture doc's illustrative table — add:

| Group | Resources | Realm |
|---|---|---|
| `/api/referrals/*` | generate/share referral link, referral status list | Host (customer-scoped) |
| `/api/businesses/{tenantId}/follow` | follow/unfollow | Host (customer-scoped) |

## Screens

**Flutter:** Referral (link/code, share sheet, status list), Favorites (followed-but-not-joined
businesses).

**Angular (business dashboard):** Followers list, with a "convert to campaign target" action feeding
into Feature 05's campaign targeting.

No mockup built yet for this feature.

## Permissions

Mostly customer-initiated (no permission beyond authenticated Host user). Business-side: `Eksabli.Followers.View`
and `Eksabli.Followers.ConvertToCampaign` (Marketing+).

## Implementation checklist

- [ ] `Referral`, `Achievement`, `AchievementAward`, `Follow` entities in `src/Eksabli.Domain/Engagement/`
- [ ] Constants in `src/Eksabli.Domain.Shared/`
- [ ] EF Core config in `EksabliDbContext`
- [ ] Migration: `dotnet ef migrations add Added_Engagement`
- [ ] Referral completion → bonus-points trigger (hooks into Feature 02's pipeline as a `Source = Referral`
      transaction) and Feature 05's notification dispatch
- [ ] `FeatureDefinitionProvider` entries for the tenant-opt-in gamification mechanics
- [ ] DTOs + `IReferralAppService`/`IFollowAppService`
- [ ] Permissions: `Eksabli.Followers.*`
- [ ] Localization keys

## Open questions

- Which specific mechanics ship in Phase 3 vs. deferred to Phase 4 needs re-confirming against
  actual retention data once Phase 1–2 are live — the phase split above is a starting sequencing
  guess, not a committed scope line.
- Spin-wheel/scratch-cards are explicitly **not** in this feature's Phase 3–4 scope — see
  [Future Features](../../07-loyalty-engine.md#future-features) for why they're deferred further out.
