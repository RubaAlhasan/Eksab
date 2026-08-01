# Feature 03 — Rewards & Redemption

[← Back to feature index](../README.md)

## Overview

The "spend points" side of the loop — a catalog customers redeem points against, and the
staff-facing confirmation flow (QR or PIN) that turns a redemption into something a cashier can
actually honor at the counter.

- **MVP phase:** 2
- **Depends on:** [02 — Membership & Wallet](../02-membership-wallet/README.md)

## Domain model

| Entity | Purpose | Key fields | Notes |
|---|---|---|---|
| `Reward` | Catalog item redeemable for points | `TenantId`, `Name`, `Type` (Discount/FreeProduct/GiftCard), `PointsCost`, `StockRemaining`, `ValidFrom/To`, `ImageBlobName` | `IMultiTenant` |
| `Coupon` | One issued, trackable instance of a redeemed reward | `RewardId`, `MembershipId`, `TenantId`, `Code`, `Status` (Issued/Redeemed/Expired/Cancelled), `IssuedAt`, `RedeemedAt`, `RedeemedByEmployeeId`, `RedeemedBranchId` | `IMultiTenant`. Unique `Code` |

Full ERD: [Database Design → Rewards & redemption](../../03-database-design.md#rewards--redemption).

## Business rules

Redemption reuses the **exact token pattern already implemented in this repo**
(`BookAppService.GetDownloadTokenAsync` / `GetListAsExcelFileAsync`, `IDistributedCache<...TokenCacheItem>`):
mint a short-lived, single-use token when a `Coupon` is issued; redemption is a server-side
transition that validates and burns the token in one step. Nothing new to invent here — same shape,
different payload.

**Approval workflow** for high-value rewards (a per-tenant configurable points threshold on
`Reward`) requires a Manager, not just any Cashier, to confirm redemption — this is a per-reward
setting, not a platform-wide rule, since a $5 coffee and a $200 gift card don't belong on the same
approval bar.

**Two redemption presentation modes**, same backend logic: QR (default — fastest at a real counter)
and PIN (fallback for low-connectivity/camera-less POS setups).

## API surface

| Group | Resources | Realm |
|---|---|---|
| `/api/rewards/*` | catalog (customer read), redeem, redemption status | Mixed — catalog read is customer-facing, redemption confirm is staff-facing |

## Screens

**Flutter:** Rewards (catalog grid), Reward Detail, Redeem Confirmation (QR/PIN + countdown),
Coupons (redemption history). See [Product Experience](../../04-product-experience.md#5-mobile-app-screen-inventory).

**Angular (business dashboard):** Rewards & Point Rules management, Coupons audit trail (filterable
by status/branch/staff — the view that surfaces redemption-fraud patterns).

**Working mockup:** the full catalog → redeem → live-countdown-QR flow is built and interactive
(tap "Redeem" on an affordable reward to see the confirmation state):
**https://claude.ai/code/artifact/1bd0c7e1-9f64-4530-b4a1-fa694accd049**

## Permissions

`Eksabli.Rewards.Create/Edit/Delete` (Owner/Manager), `Eksabli.Rewards.Redeem` (Cashier+, with the
approval-threshold escalation to Manager handled in application logic, not a separate permission).

## Implementation checklist

- [ ] `Reward`, `Coupon` entities in `src/Eksabli.Domain/Rewards/`
- [ ] Constants in `src/Eksabli.Domain.Shared/`
- [ ] EF Core config in `EksabliDbContext`, unique index on `Coupon.Code`
- [ ] Migration: `dotnet ef migrations add Added_RewardsRedemption`
- [ ] Redemption token cache item + app service methods, mirroring `BookExcelDownloadTokenCacheItem`
- [ ] DTOs + `IRewardAppService` in `Application.Contracts`, implementation in `Application`
- [ ] MiniExcelLibs-style export for the Coupons audit table (reuse existing Excel-export pattern,
      not a new one)
- [ ] Permissions: `Eksabli.Rewards.Create/Edit/Delete/Redeem`
- [ ] Localization keys

## Open questions

- Gift-card rewards that carry real stored monetary value (rather than a single-use discount/product)
  need the same ledger discipline as points if pursued — flagged in
  [Loyalty Engine §9](../../07-loyalty-engine.md#9-rewards-system) but not yet designed in detail;
  revisit if/when a business actually requests this reward type.
