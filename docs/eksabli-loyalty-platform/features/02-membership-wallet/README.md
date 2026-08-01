# Feature 02 — Membership & Wallet

[← Back to feature index](../README.md)

## Overview

The core "join a business, earn points" loop — the thing the whole platform exists to do. A
customer's `Membership` in a business carries an independent `PointsWallet`; points move only
through an append-only `PointsTransaction` ledger, never a directly-edited counter.

- **MVP phase:** 1 (join + wallet), 2 (configurable point rules, POS award-points)
- **Depends on:** [01 — Identity & Multi-Tenancy](../01-identity-multi-tenancy/README.md)

## Domain model

| Entity | Purpose | Key fields | Notes |
|---|---|---|---|
| `Membership` | Customer↔business relationship | `CustomerId`, `TenantId`, `ReferredByMembershipId`, `JoinedAt`, `Status` | `IMultiTenant`. Unique `(CustomerId, TenantId)` |
| `PointsWallet` | Current balance + lifetime stats | `MembershipId` (unique), `TenantId`, `Balance`, `LifetimeEarned`, `LifetimeRedeemed`, `CurrentTierId` | `IMultiTenant`. `Balance` is a denormalized cache — always recomputable from the ledger |
| `PointsTransaction` | Immutable ledger entry | `WalletId`, `TenantId`, `Type` (Earn/Redeem/Expire/Adjust/Refund), `Points`, `Source`, `ReferenceId`, `ExpiresAt`, `CreatedByEmployeeId` | `IMultiTenant`. Index `(TenantId, ExpiresAt)` for the expiration sweep |
| `Tier` | Per-tenant loyalty tier definition | `TenantId`, `Name`, `MinLifetimePoints`, `Multiplier` | `IMultiTenant` |
| `PointRule` | Per-tenant earning rule | `TenantId`, `RuleType` (PerCurrencyUnit/PerVisit), `PointsPerUnit` | `IMultiTenant` |

Full ERD: [Database Design → Membership & wallet](../../03-database-design.md#membership--wallet).

## Business rules

**The points pipeline** (full detail: [Loyalty Engine §8](../../07-loyalty-engine.md#8-points-system)):
base rule × tier multiplier × active campaign multiplier, plus flat bonuses (birthday, referral,
manual) as their own separate `PointsTransaction` rows — never folded into the multiplier stage, so
each bonus type stays independently reportable. **Snapshot the tier multiplier value onto the
transaction at award time** — don't just reference the tier, or a later tier-definition change
silently rewrites historical meaning. Pick and document a rounding policy (recommend floor) once,
apply it everywhere.

**Customer identification at point of award has two paths**, not one — scan the customer's wallet
QR (short-lived, single-use, tenant-bound token), or an exact-match phone-number lookup scoped
strictly to this tenant's memberships (no partial/fuzzy search — see
[System Architecture](../../02-system-architecture.md#high-level-architecture) for why a naive
"search by phone" would leak cross-tenant information). Every phone lookup is audit-logged.

**Point expiration** is itself a ledger entry (`Type = Expire`), inserted by a scheduled background
job — never a silent balance edit. **Manual adjustments** always carry `CreatedByEmployeeId` and
should be capped per-staff-per-day (fraud/error blast-radius limit).

## API surface

| Group | Resources | Realm |
|---|---|---|
| `/api/memberships/*` | join business, my memberships, my wallets | Host (customer-scoped) |
| `/api/wallet/{tenantId}/transactions` | points history for one business | Host, filtered by `CustomerId` |
| `/api/pos/award-points` | staff-initiated point award (QR or phone) | Tenant (cashier+) |

## Screens

**Flutter:** Home (wallet carousel preview), Wallet (full cross-business list), My Points
(per-business detail + tier progress), Transaction History, Join flow, QR Scanner. See
[Product Experience](../../04-product-experience.md#5-mobile-app-screen-inventory).

**Angular (business dashboard):** Award Points POS mode, Point Rules configuration, Customer
List/Detail (balance view + manual adjustment).

**Working mockup:** Home screen (wallet carousel with per-business balances and campaign badges)
and the Award Points POS screen (QR-scan/phone-lookup toggle, live points-preview calculation) are
both built and interactive:
**https://claude.ai/code/artifact/1bd0c7e1-9f64-4530-b4a1-fa694accd049**

## Permissions

`Eksabli.Memberships.*` — staff `Award` (Cashier+), `Adjust` (Manager+); customers implicitly act on
their own memberships only (no explicit permission needed, scoped by `CustomerId`).

## Implementation checklist

- [ ] `Membership`, `PointsWallet`, `PointsTransaction`, `Tier`, `PointRule` entities in
      `src/Eksabli.Domain/Memberships/` (or split into `Memberships/`, `Wallets/` if it grows)
- [ ] Constants in `src/Eksabli.Domain.Shared/`
- [ ] EF Core config in `EksabliDbContext` — all five entities `IMultiTenant`, unique index on
      `(CustomerId, TenantId)` for `Membership`
- [ ] Migration: `dotnet ef migrations add Added_MembershipWallet`
- [ ] Point-award domain service (`*Manager` suffix per convention) — the pipeline logic lives here,
      not in the application service
- [ ] DTOs + `IMembershipAppService`/`IWalletAppService` in `Application.Contracts`, implementations
      in `Application`
- [ ] QR/phone identification endpoints on the POS-facing app service, reusing the cache-token
      pattern from `BookAppService.GetDownloadTokenAsync`
- [ ] Background job: point expiration sweep
- [ ] Permissions: `Eksabli.Memberships.Award`, `.Adjust`
- [ ] Tests: the [Feature 01 proof test](../01-identity-multi-tenancy/README.md#open-questions) is
      a prerequisite — this feature's tests build on the same two-tenant fixture pattern

## Open questions

- None specific to this feature beyond what Feature 01 needs to resolve first.
