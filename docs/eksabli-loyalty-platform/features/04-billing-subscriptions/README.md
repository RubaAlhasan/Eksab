# Feature 04 — Billing & Subscriptions

[← Back to feature index](../README.md)

## Overview

How Eksabli gets paid by businesses, and how a subscription plan's limits (branches, active
members, campaigns, SMS credits) actually get enforced in the product rather than just described in
a pricing table.

- **MVP phase:** 2
- **Depends on:** [01 — Identity & Multi-Tenancy](../01-identity-multi-tenancy/README.md) (billing attaches to a `Tenant`)

## Domain model

| Entity | Purpose | Key fields | Notes |
|---|---|---|---|
| `SubscriptionPlan` | Platform-defined plan catalog | `Name`, `MonthlyPrice`, `FeatureLimits (json)` | Not tenant-scoped — platform-wide catalog |
| `TenantSubscription` | A tenant's active plan + billing state | `TenantId`, `PlanId`, `StartDate`, `RenewalDate`, `Status` (Trialing/Active/PastDue/Cancelled) | Unique `TenantId` for the current subscription |
| `Invoice` | One billing period's charge | `TenantSubscriptionId`, `Amount`, `Status`, `DueDate`, `PaidAt` | |
| `Payment` | Payment-provider transaction record | `InvoiceId`, `Provider`, `ProviderTransactionRef`, `Status` | Never store raw card data — tokenized via provider |

Full ERD: [Database Design → Billing & subscriptions](../../03-database-design.md#billing--subscriptions).

## Business rules

**Feature entitlements are ABP Feature Management, not a bespoke rules engine.**
`SubscriptionPlan.FeatureLimits` values get pushed into ABP's Feature Management per tenant on
subscription change; enforcement anywhere else in the codebase is a `RequiresFeature(...)` /
`FeatureChecker.GetAsync<int>(...)` call, not custom logic. See
[Business Strategy → Revenue model](../../01-business-strategy.md#revenue-model--pricing) for the
full reasoning and the illustrative tier table (Starter/Growth/Scale/Enterprise).

**Pricing shape:** hybrid — flat base fee per tier (bundled quota) + metered overage (extra active
members, SMS credits). **Trial, not permanent freemium** — a time-boxed 14–30 day full-featured
trial, not a permanent free tier (a permanent free tier for a loyalty product invites low-intent
signups that still cost notification/storage infra without conversion urgency).

## API surface

Not yet named as its own group in the architecture doc's illustrative API table — add:

| Group | Resources | Realm |
|---|---|---|
| `/api/billing/*` | tenant's own plan, usage-against-quota, upgrade/downgrade, invoice history | Tenant (Owner only) |
| `/api/admin/subscriptions/*` | plan catalog management, all tenant subscriptions, payment reconciliation | Host (Billing Admin) |

## Screens

**Angular (business dashboard):** Subscription & Billing (current plan + usage meters, upgrade/downgrade,
invoice history).

**Angular (admin panel):** Subscriptions & Payments (plan catalog, tenant subscription status,
manual refunds).

No mockup built yet for this feature.

## Permissions

Tenant-side billing actions restricted to Business Owner only (not Manager/Cashier/Marketing).
Platform-side restricted to Billing/Finance Admin, separate from Super Admin so billing changes are
independently auditable.

## Implementation checklist

- [ ] `SubscriptionPlan`, `TenantSubscription`, `Invoice`, `Payment` entities in
      `src/Eksabli.Domain/Billing/`
- [ ] Constants in `src/Eksabli.Domain.Shared/`
- [ ] EF Core config in `EksabliDbContext`
- [ ] Migration: `dotnet ef migrations add Added_Billing`
- [ ] `FeatureDefinitionProvider` mapping each plan's `FeatureLimits` to ABP feature definitions
- [ ] Payment provider integration (Stripe/Paddle/local — not yet chosen, see Open questions) behind
      an internal abstraction, not called directly from application services
- [ ] Background job: subscription renewal / invoice generation, tied to provider webhook confirmation
- [ ] DTOs + `IBillingAppService` in `Application.Contracts`/`Application`
- [ ] Permissions: Owner-only tenant billing actions; Billing Admin platform actions
- [ ] Localization keys

## Open questions

- **Payment provider not yet chosen.** [System Architecture](../../02-system-architecture.md#14-api-design)
  lists Stripe/Paddle/"local" as options without deciding — this should be resolved based on the
  target market's actual payment rails (relevant given the confirmed Arabic/English target market —
  regional provider support varies) before this feature starts, since it affects the `Payment`
  entity's `Provider`/`ProviderTransactionRef` shape.
- Illustrative pricing tiers in the business strategy doc are explicitly flagged as needing
  validation against the real target market, not launch-ready numbers.
