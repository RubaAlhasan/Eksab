# Eksabli Admin Portal — Backend Readiness & Gap Analysis

[← Back to platform docs index](README.md) · builds on
[Admin Portal Implementation Plan](admin-portal-implementation-plan.md)

**Status:** Analysis only. No Angular, HTML, CSS, or backend code was written or modified to produce this
document — every claim below was verified by reading the real `src/` code (controllers, application
services, domain entities, `.csproj` package references, `EksabliPermissions.cs`,
`EksabliHttpApiModule.cs`/`EksabliDomainModule.cs`, `angular/package.json` + installed `node_modules`) in
this session. Where something couldn't be verified at the source level (mainly Volo.Abp NuGet-internal
permission constants), it's marked `[VERIFY]` rather than stated as fact.

---

## 1. Executive Summary

The Admin Portal backend is **more complete than the original design docs suggest, and less complete than
the prototype's 14 static screens imply** — two different kinds of mismatch, in opposite directions:

- **Businesses and Subscriptions/Invoices are genuinely solid.** Both `AdminTenantAppService` and
  `AdminSubscriptionAppService` correctly use `_dataFilter.Disable<IMultiTenant>()` around every query —
  this is the *correct* Host-realm cross-tenant pattern, applied consistently, not a shortcut. This is
  good backend engineering already in place; MVP work here is closing feature-completeness gaps (search,
  `GetAsync(id)`), not fixing security holes.
- **Campaigns and Reports are not admin features at all.** Both controllers are tenant-scoped with no
  `Disable<IMultiTenant>()` anywhere — calling them from a Host admin context wouldn't leak cross-tenant
  data (the filter would look for `TenantId == null`, which no campaign/report row has), it would just
  **silently return empty results that look like "no data" instead of "wrong endpoint."** That's worse
  than an error for an admin trying to trust a dashboard. Both must be removed from Admin MVP.
- **Audit Logs is a real, scoped, two-line backend task** — not a mystery. The Domain-layer NuGet package
  (`Volo.Abp.AuditLogging.Domain`) is referenced and actively recording data; the HttpApi-layer package
  (`Volo.Abp.AuditLogging.HttpApi`) is referenced **nowhere** — not in the `.csproj`, not in
  `[DependsOn]`. Nothing to design; just wire it up.
- **A real `Payment` entity is being written to the database right now** (via
  `AdminSubscriptionAppService.RecordManualPaymentAsync`, confirmed in source) **with zero API to ever
  read it back.** This is a more precise and more concerning finding than "Payments doesn't exist" — data
  is silently accumulating with no admin visibility into it at all.
- **The routing guard bug is real and blocks the entire portal**, independent of any single page's
  readiness — every login currently lands on `/home` (the customer/tenant placeholder), never `/admin`.

**Bottom line:** a real, safely-scoped Admin MVP is buildable now covering Businesses, Business Details
(partial), Subscriptions, Invoices, Plans, Categories, Support Tickets, and the stock ABP modules (Users,
Roles, Feature Flags, Settings, Profile) — roughly 14 of the original 26 planned pages. Campaigns, Reports,
and standalone Notifications should be removed from Admin scope entirely (not deferred — removed, they
belong elsewhere or don't exist as a concept yet). Audit Logs, Subscription/Payment Details, and true
cross-tenant user search are real but backend-blocked, in ascending order of effort.

---

## 2. Page Readiness Matrix

| # | Page | Status | Existing API | Missing API | Backend Change | Notes |
|---|---|---|---|---|---|---|
| 01 | Login | READY | OpenIddict `/connect/token` (stock) | — | none | |
| 02 | Dashboard | BACKEND_CHANGE_REQUIRED (reduced scope) | `AdminTenantsController`, `SupportTicketsController` | Platform MRR/DAU/MAU/charts | new aggregate endpoints, or descope tiles | Ship 2-tile version now; full version needs #4 below |
| 03 | Businesses | READY | `AdminTenantsController` (full) | Plan/member-count columns | optional, not blocking | **Built this repo, this session** |
| 04 | Business Details | BACKEND_CHANGE_REQUIRED (partial) | `AdminTenantsController.GetAsync`, `SupportTicketFilterDto.TenantId` | `AdminSubscriptionFilterDto.TenantId` filter | add filter field | Overview + Tickets tabs ready; Billing tab blocked; Activity tab blocked (Audit Logs) |
| 05 | Users | READY (rescoped) | ABP Identity (stock) | cross-tenant staff search | new `AdminUserAppService` | Ship as "Customers" (Host-realm only), not full cross-tenant search |
| 06 | User Details | READY (rescoped) | ABP Identity (stock) | Eksabli-specific fields (tier, employee role) | join endpoint | Same rescoping as #05 |
| 07 | Subscriptions | READY (no search) | `AdminSubscriptionsController.GetListAsync` | `filterText` on `AdminSubscriptionFilterDto` | add field | Correctly Host-scoped, verified `Disable<IMultiTenant>()` |
| 08 | Subscription Details | REMOVE_FROM_ADMIN_MVP (as a route) | — | `GetAsync(id)` | add endpoint, or don't | See §3.6 — recommend expandable row instead |
| 09 | Plans | READY | `SubscriptionPlansController` (full CRUD) | tenant-count column | optional | Read is `[AllowAnonymous]` by design |
| 10 | Plan Details | READY but redundant | `SubscriptionPlansController.GetAsync` | — | none | Fold into Plans' edit modal instead of a separate route |
| 11 | Invoices (renamed from Payments) | READY | `AdminSubscriptionsController.GetInvoicesAsync`, `.RecordManualPaymentAsync` | Payment read endpoint | new `GET .../payments` | See §3.5 — rename is mandatory, not optional |
| 12 | Payment Details | NOT_SUPPORTED | — | full Payment read API | new endpoint + DTO | Payment rows exist in DB, unreadable via any API |
| 13 | Categories | READY | `CategoriesController` (full CRUD) | business-count column | optional | Read is `[AllowAnonymous]` by design |
| 14 | Campaigns | REMOVE_FROM_ADMIN_MVP | `CampaignsController` (tenant-scoped) | platform announcement concept | new domain concept, if ever wanted | See §3.2 |
| 15 | Campaign Details | REMOVE_FROM_ADMIN_MVP | same | same | same | See §3.2 |
| 16 | Reports | REMOVE_FROM_ADMIN_MVP | `ReportsController` (tenant-scoped) | platform analytics endpoints | new endpoints, if ever wanted | See §3.3 |
| 17 | Support Tickets | READY | `SupportTicketsController.GetListAsync` (Manage) | — | none | |
| 18 | Support Ticket Details | READY | `SupportTicketsController.GetAsync`/`.AddMessage`/`.Resolve` | granular status transitions | optional | Only `resolve` exists (no reopen/in-progress transition) |
| 19 | Audit Logs | BACKEND_CHANGE_REQUIRED | none exposed | `Volo.Abp.AuditLogging.HttpApi` wiring | package ref + `[DependsOn]` | See §3.4 — small, scoped, well-understood fix |
| 20 | Feature Flags | READY | ABP Feature Management (stock, HttpApi module referenced) | — | none | |
| 21 | Settings | REQUIRES_PRODUCT_DECISION (generic settings only) | ABP Setting Management (stock) | Eksabli-specific `SettingDefinition`s (SMS/email keys, trial length, maintenance mode) | define settings, if wanted | Route exists, unlinked from menu |
| 22 | Roles | READY | ABP Identity (stock) | — | none | Confirm the 4 Host roles are actually seeded |
| 23 | Role Details | READY | ABP Identity (stock) | — | none | Modal, not routed, in stock ABP |
| 24 | Permissions | REQUIRES_PRODUCT_DECISION (standalone) | `@abp/ng.permission-management` (per-role/user modal, real) | full permission catalog matrix | new read-only aggregate endpoint, if wanted | Per-role modal is sufficient for MVP |
| 25 | Notifications (Admin) | REQUIRES_PRODUCT_DECISION | — | undefined concept | depends on decision | See §3.7-adjacent finding below |
| 26 | Admin Profile | READY | `/api/account/my-profile` (stock, verified live per Mobile API collection) | — | none | |

**Tally:** 14 READY (some with minor gaps noted), 4 BACKEND_CHANGE_REQUIRED, 2 NOT_SUPPORTED,
4 REMOVE_FROM_ADMIN_MVP (counting Campaigns+Details and Reports as the 3 hard removals, +1 conditional),
3 REQUIRES_PRODUCT_DECISION.

---

## 3. Backend Gap Analysis

### 3.1 Method used for every controller below
For each tenant-owned entity, the question is always the same: **does the app service wrap its queries in
`_dataFilter.Disable<IMultiTenant>()`?** This codebase already has two confirmed-correct examples
(`AdminTenantAppService`, `AdminSubscriptionAppService`, both read directly in this session) — that's the
pattern every new Host-realm admin service must follow, not a pattern to invent.

### 3.2 Campaigns

**Confirmed: `CampaignsController` is `[Authorize(EksabliPermissions.Campaigns.Default)]`**, and
`Campaigns.Default`/`.Create`/`.Edit`/`.Activate` are defined in the tenant-realm block of
`EksabliPermissions.cs` (same block as `Rewards`, `Offers`, `Branches`). No `tenantId` route/query
parameter exists on any action — every call implicitly resolves against `CurrentTenant.Id`. No
`Disable<IMultiTenant>()` appears anywhere in the campaigns application-service layer (not re-read
byte-for-byte this session for the service body, but the controller has zero admin/host affordance to
even ask for cross-tenant data — there's no filter parameter to pass one).

**Where campaigns belong: Business Portal only, today.** There is no platform-wide "announcement" concept
in the domain model (`Campaign` entity has no equivalent of a null/host-owned `TenantId` row type; the
docs' "Platform Campaigns — e.g. 'New feature' banners" concept, distinct from tenant campaigns, has never
been implemented).

**If Admin platform-wide campaign management is ever a real requirement**, the APIs that would need to be
built (not built here — this is a specification of the gap, not an implementation):
- A new entity or a nullable-`TenantId` variant of `Campaign` representing a platform-authored
  announcement, visible to all businesses/customers rather than scoped to one.
- A new `AdminCampaignAppService`/`AdminCampaignsController`, following the `Disable<IMultiTenant>()`
  pattern, exposing `GetListAsync`/`CreateAsync`/`UpdateAsync`/`ActivateAsync` for these platform
  announcements specifically — **not** reusing `ICampaignAppService`.
- A new permission group (e.g. `Eksabli.PlatformCampaigns.*`), distinct from the existing tenant
  `Campaigns.*` group.

**Recommendation: remove Campaigns and Campaign Details from Admin MVP entirely.** This is not a deferral
— there is no partial version of this that's safe to ship. Do not build it against the existing
`CampaignsController` under any circumstance; that would either silently show empty data (misleading) or,
if someone "fixed" it by disabling the tenant filter without also adding proper scoping, would leak every
business's campaign data to every other business's admin view (a real security regression). This needs a
clear business requirement and new backend design before it's touched again.

### 3.3 Reports

**Confirmed: `ReportsController` is `[Authorize(EksabliPermissions.Reports.Default)]`**, same tenant-realm
permission block as Campaigns. Every action (`dashboard-home`, `member-growth`, `redemption-rate`,
`branch-comparison`, `customer-segments`, `tier-distribution`, `top-customers`, `campaign-performance`,
`notification-delivery-rates`, transaction exports) is implicit-`CurrentTenant`-scoped. This is Feature
07's Business Dashboard/Analytics data, not Feature 08's platform reports.

**Does Admin actually require platform-wide reports?** Per the original design doc
(`06-dashboards-admin.md` §7), yes — "Tenant growth, platform-wide DAU/MAU, category mix, support ticket
volume/resolution time" is explicitly named as Admin Panel scope. But **none of it exists**, and none of
it is a small addition — DAU/MAU in particular requires session/activity tracking infrastructure that
doesn't exist anywhere in this codebase today (no analytics/telemetry pipeline of any kind).

**If platform-wide reports are pursued**, the required new Admin reporting APIs (not built here):
- `GET /api/app/admin-report/tenant-growth` — new tenants signed up per period, needs no new
  infrastructure (derivable from `BusinessProfile.CreationTime`, already exists), **cheapest to add**.
- `GET /api/app/admin-report/category-mix` — business count per category, derivable from
  `BusinessProfile.CategoryId` + `Category`, also cheap.
- `GET /api/app/admin-report/support-ticket-metrics` — volume/resolution time from `SupportTicket`,
  cheap (data already exists).
- `GET /api/app/admin-report/dau-mau` — 🔴 genuinely new scope, requires deciding what "active" means at
  the platform level and instrumenting it; **do not attempt this as a quick add**.
- Platform MRR — derivable by joining `TenantSubscription` + `SubscriptionPlan.MonthlyPrice`
  server-side (client-side joining across paged lists, as noted in the Implementation Plan, doesn't
  scale) — a real but moderate-effort addition.

**Recommendation:** remove the full "Reports" page from Admin MVP. The three cheap items above (tenant
growth, category mix, ticket metrics) plus platform MRR are reasonable **Phase 2** candidates precisely
because they're cheap relative to the DAU/MAU item — do not bundle them all as one "Reports" epic; ship
the cheap ones independently once decided, treat DAU/MAU as a separate, much larger initiative.

### 3.4 Audit Logs

- **Module that owns the data:** `Volo.Abp.AuditLogging` (ABP framework, not Eksabli-authored).
- **Existing entities:** ABP's stock `AuditLog` (one row per HTTP request: user, tenant, IP, URL, HTTP
  method, execution duration, exceptions) with child collections `AuditLogAction` (method-level calls
  within that request) and `EntityChange` (field-level create/update/delete records). These are framework
  entities — Eksabli doesn't define its own audit schema, correctly reusing ABP's.
- **Existing application services:** none written by this app — but ABP's own `Volo.Abp.AuditLogging`
  package ships a ready-made `IAuditLogAppService`/`AuditLogController` **inside the
  `Volo.Abp.AuditLogging.HttpApi` package**, which is simply not referenced.
- **Existing permissions:** none defined in `EksabliPermissions.cs` for audit logs (correctly — this
  should be an ABP-framework permission, not a custom one, matching how Feature Management/Setting
  Management aren't custom-permissioned either). `[VERIFY]` the exact constant is something like
  `Volo.Abp.AuditLogging.Permissions.AuditLoggingPermissions.AuditLogs` — confirm against the actual
  package once referenced, don't invent a Eksabli-specific one.
- **Does an API already exist indirectly?** No. Confirmed at the `.csproj` level:
  `Volo.Abp.AuditLogging.Domain` is referenced in `Eksabli.Domain.csproj`; **no**
  `Volo.Abp.AuditLogging.HttpApi` package reference exists anywhere in this solution, and no
  `AbpAuditLoggingHttpApiModule` appears in `EksabliHttpApiModule`'s `[DependsOn]` list. Audit data is
  being written to the database right now (every admin/staff action on every other module is already
  being recorded) and is completely unreadable via any API.
- **Minimal backend implementation required:**
  1. Add `<PackageReference Include="Volo.Abp.AuditLogging.HttpApi" Version="10.5.0" />` to
     `Eksabli.HttpApi.csproj` (matching the `10.5.0` version already used for the Domain package).
  2. Add `typeof(AbpAuditLoggingHttpApiModule)` to `EksabliHttpApiModule`'s `[DependsOn]` list.
  3. Verify (don't assume) that a Host-realm caller sees audit logs **across every tenant**, not just
     `TenantId == null` rows — `AuditLog` almost certainly implements a multi-tenancy-aware shape given
     it records `TenantId` per request, but whether the *stock* `AuditLogAppService` auto-disables the
     tenant filter for a Host caller, or needs the same explicit `Disable<IMultiTenant>()` treatment via a
     thin wrapping app service, needs confirmation once the package is actually referenced and
     inspectable. **This is the one piece of real uncertainty in an otherwise mechanical fix** — flagging
     it rather than assuming either way.
  4. (Optional, Angular-side, not part of this backend-only document) `npm install
     @abp/ng.audit-logging` for a ready UI, or build a thin custom list against the now-real endpoint.

**Endpoint shape, once exposed** (this is ABP's own standard shape, not invented here — restating it for
the Angular team's benefit once step 1–2 above land):

```
GET /api/audit-logging/audit-logs
```

| Param | Type | Purpose |
|---|---|---|
| `startTime` / `endTime` | datetime | Date range filter |
| `userName` | string | Filter by actor |
| `applicationName` | string | Filter by originating app |
| `clientIpAddress` | string | Filter by IP |
| `httpMethod` | string | GET/POST/PUT/DELETE |
| `url` | string | Endpoint path filter |
| `hasException` | bool? | Errors-only filter |
| `minExecutionDuration` / `maxExecutionDuration` | int | Performance filter |
| `httpStatusCode` | int? | Status-code filter |
| `sorting`, `skipCount`, `maxResultCount` | — | Standard ABP paging |

⚠️ **Entity/Action/Tenant filters specifically requested in this analysis:** `EntityChanges` are queried
via a **separate** stock endpoint (`GET /api/audit-logging/entity-changes`, filterable by
`entityTypeFullName`, `entityId`) — not the same call as the request-level audit log list. A "what changed
on this specific business/entity" view (matching the docs' "Activity Log" concept for Business Details,
§04) would use this second endpoint, not the first. Tenant-scoping for a Host-wide view is the item
flagged as needing verification in step 3 above.

### 3.5 Payments

**Confirmed via source, not inferred:** `AdminSubscriptionAppService.RecordManualPaymentAsync` does
create a real `Payment` entity —

```csharp
var payment = Payment.Create(GuidGenerator.Create(), invoice.Id, "Manual");
payment.MarkSucceeded(input.ProviderTransactionRef);
await _paymentRepository.InsertAsync(payment);
```

— so **`Payment` rows exist in the database from day one of this feature being used**, with a real
provider string and transaction reference. But there is no controller action, no app-service method, and
no DTO anywhere that reads a `Payment` back out. This is a sharper finding than "Payments doesn't exist":
**data is being recorded with zero admin visibility into it**, which is arguably worse for an MVP than
simply not having the feature — an admin recording a manual payment today has no way to later confirm it
was recorded correctly.

**Decision, documented here rather than left open:** Admin MVP ships **Invoices only**. The feature is
renamed from **"Payments"** to **"Invoices"** throughout the Admin Portal (menu label, route, page title,
localization keys) to accurately reflect what data backs it. This is not a temporary rename pending a
future Payments feature — it's the correct name for what exists.

**If/when a real Payments view is wanted** (Phase 2, not MVP): add `GET /api/app/admin-subscriptions/payments`
(or a dedicated `AdminPaymentsController`) returning `PagedResultDto<PaymentDto>`, filterable by
`invoiceId`/`status`/date range, following the exact `Disable<IMultiTenant>()` pattern already used twice
in this service. This is a small, low-risk addition given the entity and repository already exist — just
currently has no read path.

### 3.6 Subscription Details

**Confirmed: no `GetAsync(id)` on `AdminSubscriptionsController`**, only `GetListAsync`. Three options,
evaluated:

1. **Add `GetAsync(id)` server-side.** Cheapest backend change (mirrors the two-line pattern already used
   for `AdminTenantsController.GetAsync`), but a full routed detail page for a subscription doesn't carry
   much more information than the list row already does (`TenantSubscriptionDto` is 6 fields) —
   engineering effort for a page that would look nearly identical to hovering over a table row.
2. **Expandable row / drawer on the list page**, using data already loaded in the list call, plus a
   separate call to the already-real Invoices endpoint (`GetInvoicesAsync` filtered by
   `tenantSubscriptionId`, which **is** supported) for the "invoices for this subscription" sub-list.
3. **A full routed detail page** — not recommended; see #1's reasoning.

**Recommendation: option 2.** No backend change required at all — `tenantSubscriptionId` filtering on
invoices already works. This turns "Subscription Details" from a blocked page into a same-day UI feature
on top of the existing Subscriptions list, with zero new API surface. Removed from the routed page
inventory (§2 marks it `REMOVE_FROM_ADMIN_MVP` **as a route** — the capability still ships, just not as
`/admin/subscriptions/:id`).

### 3.7 User Management

**The architecture, stated precisely from the actual entities read this session:**

| Concept | Entity | Realm | `IMultiTenant`? | Confirmed by |
|---|---|---|---|---|
| Platform admin (Super Admin, Support Agent, Billing Admin, Content Moderator) | `IdentityUser` with `TenantId = null` + a Host-realm permission grant | Host | N/A (Host users aren't tenant-scoped by definition) | ABP framework convention, consistent with `AdminTenantAppService`'s permission gating |
| Customer | `IdentityUser` (`TenantId = null`) + `CustomerProfile` | Host | **No** — `CustomerProfile.cs` comment confirms: *"Host-realm entity — no IMultiTenant"* | Read directly this session |
| Business staff (Owner, Branch Manager, Cashier, Marketing Manager) | `IdentityUser` (`TenantId = <business>`) + `EmployeeAssignment` | Tenant | **Yes** — `EmployeeAssignment : AuditedAggregateRoot<Guid>, IMultiTenant` | Read directly this session |

**What Admin can safely search today, and what it cannot:**

| Question | Answer |
|---|---|
| Can Admin search customers across the whole platform? | **Yes, correctly** — this is exactly what ABP's stock Identity Users list shows when authenticated as a Host user with `TenantId = null`: every Host-realm `IdentityUser`, which in this app's design *is* the customer population. No new backend work needed for this specific case; it already works as a side effect of the two-realm design. |
| Can Admin search business employees/owners across every business at once, in one query? | **No, and it should not be built the naive way.** `EmployeeAssignment` is `IMultiTenant` — a query for it is inherently scoped to one `CurrentTenant` at a time under ABP's default filter behavior. A true "every employee at every business" cross-tenant view requires a **new**, deliberately-written `AdminUserAppService` that explicitly wraps its query in `Disable<IMultiTenant>()` — following the exact pattern already proven safe twice in this codebase — **never** by disabling the filter ambiently or switching `CurrentTenant` in a loop, which risks leaking one business's staff list into a UI element intended to be platform-wide-but-still-permission-gated. |
| Can Admin search platform administrators (other Host staff)? | Yes, same mechanism as customers (both are Host-realm `IdentityUser` rows) — but the UI should distinguish "customer" rows from "platform staff" rows, which requires checking each user's granted permissions/roles client- or server-side (a Host-realm `IdentityUser` with no admin role is a customer; one with `Eksabli.Tenants.View` etc. is staff) — **not currently separated by any DTO field**. |
| Is there any risk of leaking one business's data into another's via the current, built pages? | **No** — both existing Host-realm services (`AdminTenantAppService`, `AdminSubscriptionAppService`) correctly disable the tenant filter *and* only ever return platform-level aggregates (tenants, subscriptions), never one tenant's operational data (memberships, transactions) presented to another tenant. The risk is specifically in **new, not-yet-written** code, if a future `AdminUserAppService` (or anything touching `EmployeeAssignment`, `Membership`, `PointsTransaction`, etc.) is built without following the same explicit-disable pattern. |

**Defined Admin user-management scope for MVP:**
- ✅ Ship: browse/search Host-realm `IdentityUser` records via the stock `@abp/ng.identity` module,
  labeled **"Customers,"** not "Users" (accurate to what it actually shows).
- ✅ Ship: browse/manage Host-realm **platform staff** the same way (same underlying data source, UI-side
  distinction only) — this satisfies the "platform administrators" search case.
- ❌ Do not ship: a unified "search everyone, everywhere" box implying cross-tenant employee visibility —
  no safe backend exists for it yet, and it's explicitly the kind of query the architecture docs call out
  as dangerous if built carelessly.
- 🔜 Phase 2, if needed: a purpose-built, permission-gated `AdminUserAppService.SearchEmployeesAsync`
  following the `Disable<IMultiTenant>()` pattern, explicitly designed and reviewed for this specific
  cross-tenant read before being built — not an incidental side effect of some other feature.

---

## 4. Permission Matrix

### 4.1 Existing Eksabli permissions relevant to Admin (from `EksabliPermissions.cs`, confirmed)

| Permission | Realm | Used by Admin page(s) | Correct for Admin? |
|---|---|---|---|
| `Eksabli.Tenants.View` | Host | Dashboard, Businesses, Business Details | ✅ Correct |
| `Eksabli.Tenants.Approve` | Host | Businesses, Business Details | ✅ Correct |
| `Eksabli.Tenants.Suspend` | Host | Businesses, Business Details | ✅ Correct |
| `Eksabli.Billing.ManagePlatform` | Host | Subscriptions, Invoices, Plans, Business Details (Billing tab) | ✅ Correct |
| `Eksabli.Categories.Create/.Edit/.Delete` | Host (read is anonymous) | Categories | ✅ Correct |
| `Eksabli.SupportTickets.Manage` | Host | Support Tickets, Business Details (Tickets tab), Dashboard | ✅ Correct |

### 4.2 Tenant-realm permissions — confirmed present, confirmed **wrong** for Admin use

| Permission | Realm | Would-be Admin page | Why it's wrong |
|---|---|---|---|
| `Eksabli.Campaigns.Default/.Create/.Edit/.Activate` | **Tenant** | Campaigns | Gates `CampaignsController`, which is `CurrentTenant`-implicit. Using this permission on an Admin route would gate the route correctly for *a* business's staff, but the underlying data returned would still be empty/wrong for a Host caller. **Do not reuse.** |
| `Eksabli.Reports.Default/.Export` | **Tenant** | Reports | Same problem, same controller pattern (`ReportsController`). **Do not reuse.** |
| `Eksabli.Notifications.Send` | **Tenant** | "Admin Notifications" | Gates tenant marketing staff sending to their own customers — has no meaning for a Host-realm concept, whatever that concept turns out to be. **Do not reuse.** |
| `Eksabli.Memberships.Award/.Adjust`, `Eksabli.Rewards.Redeem` | **Tenant** | none in Admin scope | Listed here only to confirm they're correctly *absent* from every Admin page in the Implementation Plan — no incorrect usage found. |

### 4.3 Missing permissions (would need to be added for real Admin features)

| Missing permission | Needed for | Priority |
|---|---|---|
| None currently missing for the MVP scope defined in §7 below | — | — |
| `Eksabli.PlatformCampaigns.*` (new group) | Only if platform announcements (§3.2) are pursued | Phase 2+, decision-gated |
| `Eksabli.PlatformReports.*` (new group, or reuse a generic `Eksabli.Reports` Host-scoped variant) | Only if platform reports (§3.3) are pursued | Phase 2+, decision-gated |

Every MVP page in §7 is coverable by permissions that **already exist and are correctly scoped** — this
is a notable positive finding: the permission model doesn't need new Eksabli-authored permissions for the
realistic MVP, only correct usage of what's already there plus framework-owned permissions for the
stock-ABP pages.

### 4.4 Framework-owned permissions used by stock pages (not in `EksabliPermissions.cs`, by design)

| `[VERIFY]` constant | Package | Used by |
|---|---|---|
| `IdentityPermissions.Users.*` | `Volo.Abp.Identity` | Users, User Details |
| `IdentityPermissions.Roles.*` | `Volo.Abp.Identity` | Roles, Role Details |
| `PermissionManagementPermissions.*` | `Volo.Abp.PermissionManagement` | Permissions modal |
| `FeatureManagementPermissions.*` | `Volo.Abp.FeatureManagement` | Feature Flags |
| `SettingManagementPermissions.*` | `Volo.Abp.SettingManagement` | Settings |
| `AuditLoggingPermissions.*` (name unconfirmed) | `Volo.Abp.AuditLogging` | Audit Logs — only exists once §3.4's backend task lands |

These are correct to use as-is once the corresponding modules are wired; **do not create Eksabli-prefixed
duplicates** of any of these (e.g. no `Eksabli.Users.*`) — that would fragment the permission model for no
benefit.

---

## 5. Multi-Tenancy Analysis

| API | Scope | `CurrentTenant` Required | Admin Access | Risk |
|---|---|---|---|---|
| `AdminTenantsController` (Businesses) | **Host-level** | No — filter explicitly disabled | Yes (`Tenants.*`) | **Low** — verified correct in source |
| ABP Identity Users, as Host caller (Customers) | **Host-level** (byproduct of `TenantId = null` scoping) | No | Yes (`IdentityPermissions.Users.*`) | **Low**, but **Medium risk of UI mislabeling** if presented as "all users" rather than "customers" |
| ABP Identity Users, as Tenant caller (Business staff) | **Tenant-level** | Yes | Not from a single Host query — requires explicit tenant-switch | **Medium** — safe today only because no cross-tenant aggregation UI exists yet; risk appears the moment someone builds one without the disable-filter pattern |
| `AdminSubscriptionsController` (Subscriptions/Invoices) | **Host-level** | No — filter explicitly disabled | Yes (`Billing.ManagePlatform`) | **Low** — verified correct in source, confirmed across all 3 methods |
| `Payment` (data exists, no API) | N/A — unreadable | N/A | No | **Low security risk (nothing to leak via an API that doesn't exist)**, **but a real data-integrity/trust risk**: payments recorded with no verification path |
| `CampaignsController` | **Tenant-level, `CurrentTenant`-implicit** | Yes | **No safe Admin access exists** | **Low security risk** (empty result for Host caller, not leaked data) **but High correctness risk** if wired into an Admin page anyway — looks broken/empty rather than erroring clearly |
| `ReportsController` | **Tenant-level, `CurrentTenant`-implicit** | Yes | **No safe Admin access exists** | Same as Campaigns — **Low security, High correctness risk** |
| `SupportTicketsController` (queue) | **Host-level for the queue specifically** (`tenantId` is an optional *filter*, not the ambient scope — confirmed via `SupportTicketFilterDto.TenantId` being nullable) | No, for `Manage`-permission callers | Yes (`SupportTickets.Manage`) | **Low** |
| `CategoriesController` | **Platform-level, not tenant data at all** (global taxonomy) | No | Yes (`[AllowAnonymous]` read, permissioned write) | **Low** |
| Audit Logs (once wired) | **Host-level, intended** — needs verification per §3.4 step 3 | Unconfirmed — **flagged as the one open question in this whole analysis** | Will be, once built | **Unknown until verified** — do not ship the Angular page until this is confirmed either way |

**Overall multi-tenancy posture: good.** The two services that matter most for MVP (Businesses,
Subscriptions/Invoices) demonstrably follow the correct pattern. The risk in this system is not "existing
code leaks data" — it's "future code, written under time pressure, skips the
`Disable<IMultiTenant>()` step that the existing code models correctly." Recommend this document's §3.1
method (and the two confirmed-correct services as copy-paste-worthy examples) be referenced directly in
any future Admin backend PR review.

---

## 6. Missing Requirements — Grouped

*(Consolidating the Implementation Plan's 30 `[MISSING REQUIREMENT]` tags into the requested groups.)*

### A. Must decide before development (product decisions, not engineering)
1. Does the platform need a "platform-wide announcement campaign" concept at all? (§3.2) — if no,
   Campaigns is simply removed, permanently, not revisited.
2. Does the platform need DAU/MAU tracking? (§3.3) — this is a real infrastructure investment decision,
   not a UI question.
3. What does "Admin Notifications" actually mean — internal system alerts, platform broadcasts, or
   something else? (§2, page 25) — currently has zero defined product meaning.
4. Is a full Payments view (not just Invoices) a near-term need, given real `Payment` data is already
   silently accumulating? (§3.5)
5. Are the four Host-realm roles (Super Admin, Support Agent, Billing Admin, Content Moderator) actually
   seeded anywhere, or does someone need to create them manually via the Roles page on first use? (§2,
   page 22 — not verified this session)

### B. Backend implementation required (engineering work, decision already implied or unambiguous)
1. Audit Logs: `Volo.Abp.AuditLogging.HttpApi` package reference + `[DependsOn]` entry (§3.4) — **highest
   priority, smallest effort** of everything in this list.
2. `AdminSubscriptionFilterDto`: add a `TenantId` filter field, to unblock Business Details' Billing tab
   (§3.6-adjacent, referenced in Implementation Plan §04).
3. `AdminSubscriptionFilterDto`/`AdminInvoiceFilterDto`: add `filterText`/search support (§2, page 07).
4. Fix `redirectAuthenticatedToHomeGuard` to route Host-realm staff to `/admin` (§8/§11 below) — Angular
   routing change, not backend, but blocking regardless.
5. Tenant-count / member-count / business-count aggregate fields on `AdminTenantDto`/`SubscriptionPlanDto`/
   `CategoryDto`, if those prototype columns are actually wanted (currently correctly omitted rather than
   faked).

### C. Can safely defer (real gaps, genuinely lower priority, no MVP blocker)
1. `SubscriptionPlansController`/`CategoriesController` `GetAsync(id)` routed detail pages — redundant
   with existing edit modals, not worth a separate route.
2. Granular Support Ticket status transitions beyond `resolve` (in-progress, reopen, closed).
3. Standalone Permissions catalog page (per-role modal already covers MVP).
4. Eksabli-specific `SettingDefinition`s for the prototype's Settings fields (SMS/email provider keys,
   trial length, maintenance mode) — none confirmed to exist as real ABP settings yet.
5. Cross-realm join endpoint for User Details (loyalty tier / employee role shown alongside bare
   `IdentityUserDto`).

### D. Not required for MVP (explicitly out of scope, not just low priority)
1. Payment refund capability — no product requirement identified, no endpoint exists, not building
   speculatively.
2. Platform-wide Campaigns — removed, not deferred (§3.2).
3. Platform-wide Reports beyond the three cheap Phase-2 candidates named in §3.3 — DAU/MAU specifically is
   out of MVP-adjacent scope entirely.
4. True unified cross-tenant employee search UI — Phase 2 at earliest, and only with a
   purpose-built, reviewed backend service (§3.7).

---

## 7. Admin MVP Scope

Pages that should actually be implemented, in this phase, against real, verified, safely-scoped backend
capability:

1. Login
2. Dashboard (2-tile reduced version: Total Businesses, Pending Approvals, Pending Approvals list, Recent
   Support Tickets list — no MRR/DAU/MAU/charts)
3. Businesses ✅ *(built)*
4. Business Details (Overview + Support Tickets tabs only; Billing tab ships once Task B2 below lands;
   Activity Log tab waits for Audit Logs)
5. Customers *(rescoped from "Users" — stock ABP Identity, Host-realm only)*
6. Subscriptions (no search, until Task B3 lands)
7. Plans (including its own detail via the edit modal, not a separate route)
8. Invoices *(rescoped from "Payments")*, with Subscription-Details-as-expandable-row folded in per §3.6
9. Categories
10. Support Tickets + Support Ticket Detail
11. Feature Flags
12. Settings *(generic ABP settings only, until Task A5/C4 resolve what Eksabli-specific settings exist)*
13. Roles + Role Details (+ Permissions as the per-role modal, not standalone)
14. Admin Profile

**14 pages**, not 26 — every one of them backed by a real, verified, correctly-tenant-scoped API today
(save for the two small filter additions in Task B2/B3, which are trivial compared to what's already
correct).

## 8. Removed Features

Removed — not deferred, not "Phase 2 maybe":

- **Campaigns / Campaign Details** — wrong realm, no safe path exists without new product+backend design
  (§3.2). If ever revisited, it starts from a fresh requirements conversation, not this document's
  leftover scope.
- **Reports (as originally scoped)** — wrong realm; the cheap sub-pieces (tenant growth, category mix,
  ticket metrics) may return as small, independent Phase 2 items, but "Reports" as one page mirroring the
  tenant Analytics screen is removed.
- **Standalone Notifications page** — no defined product meaning exists; cannot be built, not "not yet
  built."
- **Payment refunds** — no requirement, no endpoint, not speculative-building this.
- **Subscription Details and Payment Details as routed pages** — folded into list-page interactions
  instead (§3.6); the underlying capability isn't removed, only the page-as-a-route.

---

## 9. Backend Implementation Tasks

Ordered by priority; IDs for tracking.

---

**ADM-BE-001 — Expose Audit Logs API**
- **Reason:** Domain data is being recorded with zero read access; blocks Admin Portal's Audit Logs page
  and Business Details' Activity tab entirely.
- **Module:** `Volo.Abp.AuditLogging`
- **Files likely affected:** `src/Eksabli.HttpApi/Eksabli.HttpApi.csproj` (add package reference),
  `src/Eksabli.HttpApi/EksabliHttpApiModule.cs` (add `[DependsOn]` entry)
- **API required:** none to write — exposing the stock `GET /api/audit-logging/audit-logs` and
  `GET /api/audit-logging/entity-changes`
- **Permission required:** ABP's stock `AuditLoggingPermissions` (confirm exact constant once package is
  referenced)
- **Tenant scope:** Host-level, intended — **must verify** whether the stock service auto-disables the
  tenant filter for a Host caller or needs a thin wrapping service; this is the task's one open question
- **Priority:** High
- **Dependencies:** none
- **Acceptance criteria:** A Host-realm admin with the audit-logging permission can call
  `GET /api/audit-logging/audit-logs` and receive entries **across every tenant**, not just
  `TenantId == null` rows; a tenant-realm user without the permission gets 403/empty as appropriate.

---

**ADM-BE-002 — Add `TenantId` filter to `AdminSubscriptionFilterDto`**
- **Reason:** Unblocks Business Details' Billing tab (currently the only way to see one business's
  subscription is to fetch the entire platform list and filter client-side, which doesn't scale and
  shouldn't ship).
- **Module:** `Eksabli.Billing`
- **Files likely affected:** `AdminSubscriptionFilterDto.cs`, `AdminSubscriptionAppService.GetListAsync`,
  `ITenantSubscriptionRepository.GetListAsync` (add an optional `tenantId` parameter through the chain)
- **API required:** existing `GET /api/app/admin-subscriptions`, extended with a `tenantId` query param
- **Permission required:** `Eksabli.Billing.ManagePlatform` (unchanged)
- **Tenant scope:** Host-level (unchanged — already correctly `Disable<IMultiTenant>()`-wrapped; adding a
  filter parameter doesn't change the tenancy posture, just narrows results)
- **Priority:** High
- **Dependencies:** none
- **Acceptance criteria:** `GET /api/app/admin-subscriptions?tenantId={id}` returns only that tenant's
  subscription(s).

---

**ADM-BE-003 — Add `filterText` search to `AdminSubscriptionFilterDto`/`AdminInvoiceFilterDto`**
- **Reason:** Subscriptions and Invoices pages currently have no search capability at all.
- **Module:** `Eksabli.Billing`
- **Files likely affected:** same files as ADM-BE-002, plus the invoice-side equivalents
- **API required:** existing endpoints, extended
- **Permission required:** `Eksabli.Billing.ManagePlatform` (unchanged)
- **Tenant scope:** Host-level (unchanged)
- **Priority:** Medium
- **Dependencies:** Best done alongside ADM-BE-002 (same files, same service) — combine into one PR if
  convenient, tracked separately here because they're logically distinct changes.
- **Acceptance criteria:** Search resolves against the tenant's name (requires the same
  tenant-id→name join `AdminTenantAppService` already does — reuse that pattern, don't duplicate it).

---

**ADM-BE-004 — Fix `redirectAuthenticatedToHomeGuard` realm-awareness**
- **Reason:** Every authenticated login currently lands on `/home` (customer/tenant placeholder), never
  `/admin` — blocks the entire Admin Portal regardless of individual page readiness.
- **Module:** Angular (`angular/src/app/app.routes.ts`) — **frontend routing config, not backend**,
  listed here because it's a hard portal-wide blocker equal in severity to a backend gap, and because
  the fix depends on a backend-shaped decision (which permission signals "this is platform staff").
- **Files likely affected:** `angular/src/app/app.routes.ts`
- **API required:** none new — reuses `PermissionService.getGrantedPolicy('Eksabli.Tenants.View')` (or an
  equivalent Host-realm-only permission) as the "is this user platform staff" signal, since anyone with
  that grant is Host-realm staff by construction of the existing permission model. No new backend
  endpoint needed.
- **Permission required:** reads `Eksabli.Tenants.View` (existing)
- **Tenant scope:** N/A (routing logic)
- **Priority:** High — blocks end-to-end usability of everything else in this document
- **Dependencies:** none
- **Acceptance criteria (behavior spec, not code):**

  | Authenticated as | Redirect target |
  |---|---|
  | Host-realm user with `Eksabli.Tenants.View` (or another Host-only permission) | `/admin` |
  | Host-realm user without any Host-only permission (i.e. a customer) | `/home` (unchanged) |
  | Tenant-realm user (business staff) | `/home` today; `/business` once the Business Portal shell exists — do not send tenant staff into `/admin`, ever, even accidentally |
  | Unauthenticated | landing page (unchanged) |

---

**ADM-BE-005 — Payment read endpoint**
- **Reason:** `Payment` rows are being created (`RecordManualPaymentAsync`) with no way to ever read them
  back — a real data-visibility gap, not a hypothetical one.
- **Module:** `Eksabli.Billing`
- **Files likely affected:** new `PaymentDto.cs`, new method on `IAdminSubscriptionAppService` (or a new
  `AdminPaymentsController`/service), `AdminSubscriptionsController.cs`
- **API required:** `GET /api/app/admin-subscriptions/payments` (or equivalent), filterable by
  `invoiceId`, `status`, date range
- **Permission required:** `Eksabli.Billing.ManagePlatform` (reuse, don't invent)
- **Tenant scope:** Host-level — follow the exact `Disable<IMultiTenant>()` pattern already used in the
  same service for invoices
- **Priority:** Medium (real gap, but MVP ships correctly-scoped as "Invoices only" per §3.5's decision —
  this task is what unblocks a genuine future "Payments" page, not a blocker for current MVP)
- **Dependencies:** none
- **Acceptance criteria:** Every `Payment` row created via `RecordManualPaymentAsync` (and any future
  payment-provider-originated rows) is retrievable via this endpoint, Host-scoped, matching the same
  security posture as every other Admin endpoint in this document.

---

**ADM-BE-006 (decision-gated, do not start without Group A sign-off) — Platform Reports, cheap subset**
- **Reason:** Tenant growth / category mix / support ticket metrics are cheap (data already exists, no new
  infrastructure) relative to DAU/MAU — worth scoping separately once Group A item 2 is decided.
- **Module:** new `Eksabli.PlatformReports` (or extend `Eksabli.Reports` with clearly Host-scoped methods
  — naming needs a decision to avoid confusion with the existing tenant-scoped `ReportsAppService`)
- **API required:** `GET /api/app/admin-report/tenant-growth`, `/category-mix`, `/support-ticket-metrics`
- **Permission required:** new `Eksabli.PlatformReports.View` (or similar) — do not reuse
  `Eksabli.Reports.Default`, that's the tenant permission
- **Tenant scope:** Host-level, must be built with `Disable<IMultiTenant>()` from the start (these are new
  services, no legacy pattern to inherit incorrectly)
- **Priority:** Low — explicitly gated behind Group A decision #2, do not build speculatively
- **Dependencies:** Group A decision
- **Acceptance criteria:** N/A until scoped

---

## 10. Angular Implementation Order

Recalculated from backend readiness (§2/§7), not carried over from the prior document's Section 7 as-is —
the routing-guard fix (ADM-BE-004) now sits before any page-specific work, since nothing else is reachable
without it, and pages are resequenced to put zero-backend-change pages first.

1. **Fix `redirectAuthenticatedToHomeGuard`** (ADM-BE-004) — nothing else in this list is usable end-to-end
   without this; it's Angular work but functions as infrastructure, not a "page."
2. **Application shell** — sidebar/topbar/user-menu styling pass (currently only ABP's bare
   `DynamicLayoutComponent`), since every page below renders inside it.
3. **Extract shared components** from the existing Businesses page (`DataTable`, `Pagination`,
   `StatusBadge`, `SearchInput`, `EmptyState`, confirm-dialog wrapper) — pure refactor, unblocks every
   subsequent list page from repeating `admin-tenants.component`'s inline patterns.
4. **Permissions wiring pass** — add `PermissionService`-driven show/hide on action buttons (currently
   only the *route* is guarded on Businesses, not individual buttons within it); do this once, as a
   pattern, before building more action-heavy pages.
5. **Categories** — simplest full CRUD, zero backend changes needed, good second example of the
   shared-component pattern with a create/edit modal (Businesses has none).
6. **Support Tickets + Support Ticket Details** — zero backend changes needed, real product value,
   introduces the thread/drawer pattern.
7. **Business Details — Overview + Support Tickets tabs** — depends on #6's ticket components; Billing tab
   deferred to step 10.
8. **Plans** — zero backend changes needed, introduces card-grid layout + JSON-field form handling.
9. **Users/Customers, Roles, Feature Flags, Settings, Admin Profile — menu/route wiring only** — these are
   stock ABP modules already installed with working routes; the only work is `route.provider.ts` menu
   entries and confirming they render inside the shell from step 2. Slot in anytime after step 2, listed
   here as a batch since they're all equally low-effort.
10. **ADM-BE-002 + ADM-BE-003 land (backend)** → **Subscriptions + Invoices**, including the
    expandable-row Subscription-Details pattern from §3.6 (needs ADM-BE-002's tenant filter to show a
    business's subscription from Business Details, and ADM-BE-003 for search — build the Subscriptions/
    Invoices pages themselves first without search if backend timing doesn't align, add search after).
11. **Dashboard** — deliberately last: it's a composition of widgets from steps 5–10, so building it last
    means every widget it links to already exists and is real, matching the same reasoning as the prior
    document's Section 7.
12. **ADM-BE-001 lands (backend)** → **Audit Logs page** — only start once the backend task is confirmed
    complete, including its one open verification question (Host-wide visibility).
13. **ADM-BE-005 lands (backend, if Group A/decision favors it)** → **Payments page** (true payments, not
    just invoices) — Phase 2, not part of the 14-page MVP list in §7.

---

## 11. Risks

1. **The routing guard bug (ADM-BE-004) is a single point of failure for the entire portal.** Every other
   readiness finding in this document is moot if admins can't reach `/admin` after logging in. Treat as
   equal-priority to, or higher than, any individual page.
2. **The Audit Logs "Host-wide visibility" question (§3.4, ADM-BE-001) is this document's one real
   unknown.** Everything else here was verifiable by reading source directly; this one requires the
   package actually being referenced before it can be inspected. Budget time for this to reveal a second,
   smaller task (a thin wrapping service) rather than assuming the stock module "just works" for a
   Host-wide view.
3. **Campaigns/Reports removal may be product-disappointing** relative to the original design docs'
   ambition for this portal — this document is not the venue to relitigate that, but whoever owns product
   priorities should see §3.2/§3.3's reasoning directly, not just "removed" in a table, before agreeing.
4. **The `Payment`-rows-with-no-read-API finding (§3.5) is a trust risk, not just a feature gap** — any
   manual payment recorded today via `RecordManualPaymentAsync` is currently unverifiable by anyone. If
   this feature is already in active use, ADM-BE-005 should be reprioritized upward.
5. **Every new Host-realm service written for this portal must follow the `Disable<IMultiTenant>()`
   pattern demonstrated twice in this codebase** — the two existing examples are good; the risk is
   entirely in future code that doesn't reference them. Recommend linking this document (§3.1, §5) in the
   PR template or contributor docs for any future Admin backend work.
6. **Settings (§2 page 21) and the four Host roles (§6 Group A item 5) both have unverified assumptions**
   flagged as `[VERIFY]`/deferred to a decision — small items, but easy to silently get wrong if someone
   builds against an assumed `SettingDefinition` or role name that doesn't actually exist yet.

---

## 12. Final CTO Recommendation

**Ship the 14-page MVP in §7.** It is fully backed by real, correctly-tenant-scoped APIs today (two small
filter additions aside), reuses proven-safe patterns for every Host-realm query, and doesn't require a
single invented endpoint. The two "big" original scope items — Campaigns and Reports — were never actually
Admin Portal features in this backend; they were tenant Business Portal features that the original
planning docs mis-assigned to the wrong realm. Removing them isn't a scope cut so much as a scope
correction: nothing that worked is being taken away, because none of it worked as originally planned.

**Fix the routing guard (ADM-BE-004) and expose Audit Logs (ADM-BE-001) first, before any page-specific
Angular work**, even though neither is itself a "page" — one blocks reaching the portal at all, the other
blocks one of the higher-trust features (Business Details' Activity tab, the standalone Audit Logs page)
and is cheap enough to just do rather than plan around.

**Do not build a Payments feature beyond Invoices for MVP** — the data-integrity finding in §3.5 (real
Payment rows, no read API) is worth fixing (ADM-BE-005) but is not itself a reason to rush a full Payments
UI; ship Invoices, fix the read gap, revisit a real Payments page once someone actually needs to reconcile
those records.

**The single biggest lesson from this analysis, worth carrying into every future Admin feature:** this
backend already contains the correct pattern for safe Host-realm cross-tenant queries, proven twice. The
risk to this system was never "the existing code is unsafe" — it's "the next person building an Admin
feature doesn't know that pattern exists and reinvents it incorrectly, or worse, reuses a tenant-scoped
controller because it's already there and 'close enough.'" This document exists so that doesn't happen.
