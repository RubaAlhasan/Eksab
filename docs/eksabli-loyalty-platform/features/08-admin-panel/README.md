# Feature 08 — Admin Panel

[← Back to feature index](../README.md)

## Overview

Platform-operations tooling for the Eksabli team itself (not businesses): tenant oversight, support,
platform-wide reporting, feature flags, audit logs. Lives in the **same Angular app** as the
business dashboard ([Feature 07](../07-business-dashboard/README.md)), gated to Host-realm users by
permission — not a separate codebase. See
[System Architecture](../../02-system-architecture.md#high-level-architecture) for why building a
4th surface here would be unnecessary complexity.

- **MVP phase:** 4
- **Depends on:** [01 — Identity & Multi-Tenancy](../01-identity-multi-tenancy/README.md) (Host realm, Tenant management), [04 — Billing & Subscriptions](../04-billing-subscriptions/README.md)

## Domain model

| Entity | Purpose | Key fields | Notes |
|---|---|---|---|
| `Category` | Business category taxonomy for discovery | `Name`, `IconBlobName`, `ParentCategoryId` | Self-referencing for subcategories |
| `SupportTicket` | Platform support | `TenantId` (nullable), `CustomerId` (nullable), `Subject`, `Status`, `Priority` | |
| `SupportTicketMessage` | Thread on a ticket | `TicketId`, `SenderId`, `Body`, `CreatedAt` | Child of `SupportTicket` — no separate repository |

Everything else this feature surfaces (audit logs, feature flags, system settings) is **already
provided by ABP modules already in this repo** — `AbpAuditLogging`, `Feature Management`,
`Setting Management` — this feature just puts a thin UI over them, it doesn't reimplement them. See
[Database Design → Platform & ops](../../03-database-design.md#platform--ops).

## Business rules

**"View-as-tenant" support tooling must be fully audited, never silent** — a Support Agent looking
at a business's data on their behalf is exactly the kind of action that needs a clear audit trail
(who looked at what, when, why).

**Manual tenant approval until self-serve moderation tooling exists** — don't auto-publish new
businesses to customer-facing search/discovery before a Content Moderator can review the profile;
this is a Phase 4 gate, not a permanent state, but the platform shouldn't launch self-serve signup
without it. See [Risks](../../01-business-strategy.md#21-risks).

## API surface

| Group | Resources | Realm |
|---|---|---|
| `/api/admin/tenants/*` | approve/suspend, view (list + detail) | Host, Super Admin |
| `/api/admin/subscriptions/*` | see [Feature 04](../04-billing-subscriptions/README.md) | Host, Billing Admin |
| `/api/admin/support-tickets/*` | queue, thread, resolve | Host, Support Agent |

## Screens

**Angular:** Tenant Management (list, approve/suspend, view-as-tenant), User Management
(cross-tenant search — Host-only, exactly the query [Feature 01](../01-identity-multi-tenancy/README.md)
says must never be exposed to tenant-side staff), Subscriptions & Payments (shared with Feature 04),
Platform Campaigns (platform-wide announcements, distinct from tenant `Campaign`s), Categories,
Support Tickets, Reports (platform-wide analytics), Feature Flags (thin UI over ABP Feature
Management), Audit Logs (thin UI over ABP Audit Logging), System Settings.

No mockup built yet for this feature.

## Permissions

Super Admin (full), Support Agent (read-mostly + capped goodwill point adjustments), Billing/Finance
Admin (billing only — separate from Super Admin for auditability), Content Moderator (business
profile/campaign approval — only needed once self-serve signup ships).

## Implementation checklist

- [ ] `Category`, `SupportTicket`, `SupportTicketMessage` entities in `src/Eksabli.Domain/Platform/`
- [ ] Constants in `src/Eksabli.Domain.Shared/`
- [ ] EF Core config in `EksabliDbContext`
- [ ] Migration: `dotnet ef migrations add Added_AdminPanel`
- [ ] DTOs + `ISupportTicketAppService`/`ICategoryAppService`
- [ ] Angular routing/permission gating for Host-only views within the existing dashboard app
- [ ] Permissions: `Eksabli.Admin.*` role definitions from the [role table](../01-identity-multi-tenancy/README.md#permissions)
- [ ] Localization keys

## Open questions

- None specific beyond the platform-wide risk items already tracked in
  [Business Strategy §21](../../01-business-strategy.md#21-risks).
