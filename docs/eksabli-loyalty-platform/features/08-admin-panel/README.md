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
| `SmsLog` | Dev/testing record of every SMS `NullSmsSender` "sent" (OTP codes + campaign SMS) | `PhoneNumber`, `Message`, `CreationTime` | Host-realm, no `IMultiTenant` — written only by `NullSmsSender`, expected to stop growing once a real `ISmsSender` replaces it. Browsable at Admin Portal → Verification Codes (`Eksabli.SmsLogs` permission) |

Everything else this feature surfaces (audit logs, feature flags, system settings) is **already
provided by ABP modules already in this repo** — `AbpAuditLogging`, `Feature Management`,
`Setting Management` — this feature just puts a thin UI over them, it doesn't reimplement them. See
[Database Design → Platform & ops](../../03-database-design.md#platform--ops). In practice, Audit
Logs shipped as a small **hand-written** query surface (`AdminAuditLogAppService`/`AuditLogsController`)
over the same underlying `Volo.Abp.AuditLogging.Domain` data, rather than referencing the stock
`Volo.Abp.AuditLogging.HttpApi` package — both are valid resolutions of the same gap; this repo took
the hand-written path.

`EksabliSettingDefinitionProvider`/`EksabliSettings` (`src/Eksabli.Domain/Settings/`) now define real,
Eksabli-specific settings on top of ABP's stock Setting Management module — `Trial.LengthDays` (read by
`BusinessAppService.ProvisionTrialSubscriptionAsync`, replacing the fixed `BillingConsts
.TrialDurationDays` constant as the effective value), `MaintenanceMode`, `Sms.ActiveProvider`. Real
provider credentials (SMS/email API keys) deliberately stay out of Settings — those values are
DB-stored and editable via the Setting Management admin UI, not where secrets belong; they'd live in
`appsettings.json`/`IConfiguration` instead, same as `Fcm:CredentialsFilePath` already does.

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
| `/api/admin/subscriptions/*` | see [Feature 04](../04-billing-subscriptions/README.md) — also `GET .../payments` (filterable by `invoiceId`), closing the gap where `RecordManualPaymentAsync` wrote `Payment` rows with no read path | Host, Billing Admin |
| `/api/admin/support-tickets/*` | queue, thread, resolve | Host, Support Agent |
| `/api/app/admin-platform-reports/*` | `tenant-growth` (monthly, trailing 7 months), `ticket-metrics` (volume by status/priority — **not** resolution time, see Open questions) | Host, `Eksabli.PlatformReports` |
| `/api/app/admin-sms-logs` | list/clear `SmsLog` | Host, `Eksabli.SmsLogs` |

## Screens

**Angular:** Tenant Management (list, approve/suspend, view-as-tenant), User Management
(cross-tenant search — Host-only, exactly the query [Feature 01](../01-identity-multi-tenancy/README.md)
says must never be exposed to tenant-side staff), Subscriptions & Payments (shared with Feature 04),
Categories, Support Tickets, Reports (platform-wide, cheap subset — see below), Feature Flags (thin UI
over ABP Feature Management), Audit Logs, System Settings, Verification Codes (SMS Logs).

**Platform Campaigns is explicitly out of scope, not deferred** — the only `Campaign` concept this
codebase has is tenant-scoped (`CampaignsController`, `CurrentTenant`-implicit, in the Business
Portal), and there is no platform-wide-announcement domain concept to build an Admin equivalent
against. Building one would require new domain design first, not just a new screen.

**Reports** shipped as the "cheap" subset only, deliberately: tenant growth (`BusinessProfile
.CreationTime`, trailing 7 months, zero-filled) and support-ticket volume by status/priority
(`AdminPlatformReportAppService`, `Eksabli.PlatformReports` permission — its own group, not a reuse of
the tenant-realm `Reports.*` permissions, which gate a `CurrentTenant`-implicit controller that would
silently return empty data for a Host caller). Category mix and MRR are **not** recomputed for this
page — it calls the same real endpoints the Dashboard already uses (`CategoryDto.businessCount`,
`AdminSubscriptionAppService.GetStatsAsync`/`GetMrrTrendAsync`) rather than shipping a second,
potentially-divergent copy. DAU/MAU and ticket resolution-time are still not built — see Open questions.

No mockup built yet for this feature.

## Permissions

Super Admin (full), Support Agent (read-mostly + capped goodwill point adjustments), Billing/Finance
Admin (billing only — separate from Super Admin for auditability), Content Moderator (business
profile/campaign approval — only needed once self-serve signup ships).

## Implementation checklist

- [x] `Category`, `SupportTicket`, `SupportTicketMessage` entities in `src/Eksabli.Domain/Platform/`
- [x] Constants in `src/Eksabli.Domain.Shared/`
- [x] EF Core config in `EksabliDbContext`
- [x] Migration: `dotnet ef migrations add Added_AdminPanel`
- [x] DTOs + `ISupportTicketAppService`/`ICategoryAppService`
- [x] Angular routing/permission gating for Host-only views within the existing dashboard app
- [x] Permissions — not a single `Eksabli.Admin.*` group as originally sketched; each Host-only
      capability got its own group instead (`Tenants`, `Categories`, `SupportTickets`, `AuditLogs`,
      `SmsLogs`, `PlatformReports`, `Billing.ManagePlatform`, `Users`), each explicitly restricted via
      `MultiTenancySides.Host` — see [Feature 01](../01-identity-multi-tenancy/README.md#permissions)
      and `EksabliPermissionDefinitionProvider`'s own comment on why the `Host`-only restriction is
      load-bearing, not decorative (a real cross-tenant grant leak was caught live without it).
- [x] Localization keys

## Open questions

- **DAU/MAU** — no session/activity-tracking infrastructure exists anywhere in this codebase. A real
  platform-wide Reports page could add this, but it's a standalone infrastructure investment, not a
  small addition to the Reports feature above — treat as a separate initiative if it's ever pursued.
- **Support-ticket resolution-time metrics** — `SupportTicket` has no `ResolvedAt`/`ClosedAt` field;
  `LastModificationTime` isn't a safe proxy (`AddMessage` bumps it on every reply, not just on
  resolution). Add a real column first if this metric is wanted; the Reports page ships volume-only
  until then.
- **Platform-wide Payments** (beyond the Invoices-scoped read added to `/api/admin/subscriptions/*`) —
  not pursued; no product requirement identified for a dedicated cross-invoice Payments view.
- Otherwise none specific beyond the platform-wide risk items already tracked in
  [Business Strategy §21](../../01-business-strategy.md#21-risks).
