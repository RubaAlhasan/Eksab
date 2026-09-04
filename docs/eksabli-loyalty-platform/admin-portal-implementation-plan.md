# Eksabli Admin Portal — Angular Implementation Plan

[← Back to platform docs index](README.md)

> ⚠️ **SUPERSEDED — historical planning snapshot, not current status.** This document was written when
> only the Businesses page existed. The Admin Portal has since shipped well past this plan's 14-page
> MVP — Audit Logs, Notifications, SMS Logs, and a Platform Reports page all now exist, and the routing
> bug this doc flags (every login landing on `/home` instead of `/admin`) is fixed. For what's actually
> built today, see [`features/08-admin-panel/README.md`](features/08-admin-panel/README.md). Kept here
> as a record of the original reasoning, not as a live status doc — don't plan new work from this file.

**Status:** Planning only — no Angular/HTML/CSS in this document, per the request that produced it.
**Audience:** whoever implements the Admin Portal Angular screens next.

## Sources used (and what was rejected as a source)

This plan is grounded in the **real, currently-committed backend** — not the design docs' aspirational
scope, and not the Mobile API Postman collection (that collection is Host-realm **customer-facing** only;
it has zero overlap with Admin Portal needs). Concretely, this document was built by reading, in this
order:

1. `src/Eksabli.Application.Contracts/Permissions/EksabliPermissions.cs` — the full, real permission tree.
2. Every relevant controller in `src/Eksabli.HttpApi/Controllers/` (`AdminTenantsController`,
   `AdminSubscriptionsController`, `SubscriptionPlansController`, `CategoriesController`,
   `SupportTicketsController`, `ReportsController`, `CampaignsController`, `NotificationsController`,
   `BillingController`).
3. The DTOs those controllers actually accept/return (`src/Eksabli.Application.Contracts/{Billing,Platform,Businesses}/*.cs`).
4. `src/Eksabli.Domain/EksabliDomainModule.cs` and `src/Eksabli.HttpApi/EksabliHttpApiModule.cs` — which
   ABP framework modules are actually referenced (this is how the Audit Logs gap below was found: the
   *Domain* module for audit logging is referenced, but the *HttpApi* module that would expose it over
   REST is not).
5. `angular/package.json` + `angular/node_modules/@abp/*` — which ABP Angular packages are actually
   installed (confirms `@abp/ng.identity`, `@abp/ng.tenant-management`, `@abp/ng.setting-management`,
   `@abp/ng.feature-management`, `@abp/ng.permission-management` (transitive), `@abp/ng.account`; confirms
   **no** `@abp/ng.audit-logging`).
6. `angular/src/app/admin/businesses/admin-tenants.component.*` — the one Admin Portal screen that
   already exists (built and `ng build`-verified in this repo, this session), used as the working pattern
   for every other custom page below.
7. `prototype/admin/*.html` — the 14-screen static HTML prototype, used only for **visual/UX** reference
   (layout, copy tone, which fields matter to show), never as an API source.

**Every `[MISSING REQUIREMENT]` tag below is a real, verified gap** — confirmed absent by reading the
actual controller/DTO/module source, not a guess. Where an ABP-framework-owned permission constant
(Identity/FeatureManagement/SettingManagement) is referenced, it's flagged `[VERIFY — FRAMEWORK
PERMISSION]` because those constants live in Volo.Abp NuGet packages this session didn't grep at the
source level (unlike the npm packages, which were checked directly) — high confidence from ABP's stable
public API, but confirm the exact string at implementation time rather than trusting this doc blindly.

## Legend used throughout

| Tag | Meaning |
|---|---|
| ✅ Real | Endpoint/permission/package verified to exist as described |
| ⚠️ Partial | Exists but with a real limitation (missing filter, missing GetAsync, etc.) |
| 🔴 `[MISSING REQUIREMENT]` | No backend capability exists for this; flagged, not invented |
| 🧩 Stock ABP | This page is (or should be) a thin wrapper over an already-installed ABP Angular module, not custom-built |

---

# 01 — Login

### 1. Page Overview
- **Page name:** Admin Login
- **Route:** `/account/login` (ABP's own account module route — already registered in `app.routes.ts` via
  `loadChildren: () => import('@abp/ng.account').then(c => c.createRoutes())`)
- **Purpose:** Authenticate a Host-realm platform user (Super Admin / Support Agent / Billing Admin /
  Content Moderator) via OpenIddict.
- **Primary user:** Any Host-realm staff role.
- **MVP or Future:** MVP.

### 2. User Journey
Unauthenticated visitor hits any `/admin/*` route → `authGuard` redirects to `/account/login` → submits
credentials → OpenIddict password grant issues tokens → redirected back to originally-requested route, or
to `/home` by default (see `redirectAuthenticatedToHomeGuard` in `app.routes.ts` — **note:** this guard
currently sends everyone to `/home`, not `/admin`; see Risks).

### 3. Permissions
None — authentication is a precondition for permissions, not gated by one. `[AllowAnonymous]` by
definition.

### 4. API
🧩 Stock ABP — uses OpenIddict's `/connect/token` endpoint exactly as documented in
`postman/gen/mobile.js`'s Authentication folder (`grant_type=password`, form-urlencoded, `client_id=Eksabli_App`).
This is framework/OAuth2 plumbing, not an Eksabli app service — `@abp/ng.oauth` (installed) handles the
token exchange; no custom HTTP call needed.

| Method | Endpoint | Purpose | Auth |
|---|---|---|---|
| POST | `{{issuerUrl}}/connect/token` | Password grant login | none (`noauth`) |

### 5. UI Layout
Centered card, no sidebar/topbar chrome (`eLayoutType.empty` — confirmed pattern from
`NEXT_SESSION_PROMPT.md` gotcha #2: unregistered/account routes default to `empty` layout automatically).
Email/username + password fields, submit button, error banner on failure.

### 6. Components
🧩 Provided entirely by `@abp/ng.account`'s `LoginComponent`. No custom component needed unless the
brand wants a fully custom-styled login screen (matches `prototype/admin/login.html`'s MFA-flavored visual
design) — if so, that's a **design decision**, not a missing-backend gap: 🔴 `[MISSING REQUIREMENT]` —
**MFA/two-factor login shown in the prototype has no backend support today** (no OTP-for-staff grant,
unlike the customer-facing `otp` grant which exists). Building the prototype's MFA step would be pure
UI theater against nothing. Recommend: ship the plain ABP login for MVP, drop the MFA UI from admin scope
until a real second factor exists server-side.

### 7. Table
N/A.

### 8. Forms
| Field | Type | Required | Validation | Default | API field | Error message |
|---|---|---|---|---|---|---|
| Username/Email | text | yes | non-empty | — | `username` | "Enter your username or email." |
| Password | password | yes | non-empty | — | `password` | "Enter your password." |

### 9. Actions
| Action | Button label | Permission | API | Confirm? | Success | Error |
|---|---|---|---|---|---|---|
| Login | "Sign In" | none | `POST /connect/token` | no | redirect | "Incorrect username or password." (maps OAuth2 `invalid_grant`) |

### 10. States
Initial (empty form) → Submitting (button spinner) → Error (inline banner, matches OAuth2
`{error, error_description}` shape already documented in the Mobile API collection) → Success (redirect).
No skeleton/empty-table states apply to this page.

### 11. Responsive Design
Single-column centered card at every breakpoint; no layout changes needed (ABP's stock component already
handles this).

### 12. Dependencies
None — entry point.

### 13. Acceptance Criteria
- Admin can log in with a valid Host-realm account.
- Invalid credentials show an inline error, not a silent failure.
- An already-authenticated visitor hitting `/account/login` is redirected away (ABP default behavior).
- A Tenant-realm (business staff) account should **not** be able to reach any `/admin/*` route after
  login — this must be enforced by permission checks on each Admin page (Host-realm staff are granted
  `Eksabli.Tenants.*` etc.; tenant staff are not), not by the login page itself.

---

# 02 — Dashboard

### 1. Page Overview
- **Page name:** Admin Dashboard
- **Route:** `/admin` (or `/admin/dashboard` — pick one; this doc uses `/admin`)
- **Purpose:** At-a-glance platform health for a Super Admin landing after login.
- **Primary user:** Super Admin (any Host role realistically glances at this).
- **MVP or Future:** MVP, but **scope must shrink to what's real** — see below.

### 2. User Journey
Admin logs in → lands here (once routing is fixed per Risk in §01) → scans stat tiles and two "recent
activity" widgets → clicks into Businesses or Support Tickets to act.

### 3. Permissions
No dedicated `Dashboard` permission exists in `EksabliPermissions.cs`. Recommend gating the route with
`Eksabli.Tenants.View` (the closest real "you're platform staff" signal) rather than inventing a new
permission — or leave it behind `authGuard` only and let each *widget* fail gracefully if the viewer
lacks the permission behind it (e.g. hide the Support Tickets widget entirely for a viewer without
`SupportTickets.Manage`).

### 4. API
| Widget | Method | Endpoint | Permission | Status |
|---|---|---|---|---|
| Total businesses / pending approvals count | GET | `/api/app/admin-tenants?approvalStatus=0&maxResultCount=1` (read `totalCount`) | `Eksabli.Tenants.View` | ✅ Real (reuses `AdminTenantsService.getList`, same as Businesses page) |
| Recent support tickets | GET | `/api/app/support-ticket?maxResultCount=5&sorting=creationTime desc` | `Eksabli.SupportTickets.Manage` | ✅ Real |
| Platform MRR | — | — | — | 🔴 `[MISSING REQUIREMENT]` — no aggregate revenue endpoint. `AdminSubscriptionsController` returns a *list* of subscriptions with plan names but no price rollup (price lives on `SubscriptionPlanDto.MonthlyPrice`, would need client-side join across two paged lists — expensive and inaccurate at scale). |
| DAU / MAU | — | — | — | 🔴 `[MISSING REQUIREMENT]` — no analytics/telemetry endpoint of any kind exists in this backend. |
| Category mix chart | — | — | — | 🔴 `[MISSING REQUIREMENT]` — `CategoriesController` has no "business count per category" aggregate; would require N+1 calls against `AdminTenantsService` filtered by category, which doesn't even support a category filter today (`AdminTenantFilterDto` only has `approvalStatus`/`filterText`). |
| Tenant growth trend | — | — | — | 🔴 `[MISSING REQUIREMENT]` — no time-series endpoint. `ReportsAppService.GetMemberGrowthAsync` exists but is **tenant-scoped** (a business's own member growth), not platform tenant-signup growth. |

### 5. UI Layout
Breadcrumb (Admin Portal / Dashboard) → 2-tile stat row (Total Businesses, Pending Approvals — both real)
→ two-column "Pending Approvals list" + "Recent Support Tickets list" (both real, both link out to their
full pages). **Do not build the MRR tile, DAU/MAU tile, category-mix chart, or growth chart** against real
data for MVP — either omit them or clearly label them "Coming soon" rather than rendering static/fake
numbers in a page that otherwise looks live.

### 6. Components
`PageHeader`, `StatCard` (×2 for MVP, not 4), `DataListWidget` (reusable small list-with-link-out, used
for both Pending Approvals and Recent Tickets).

### 7. Table
N/A (list widgets, not a full DataTable — no pagination/sort/filter needed for a 5-row preview).

### 8. Forms
N/A.

### 9. Actions
| Action | Button label | Permission | API | Confirm? |
|---|---|---|---|---|
| View all businesses | "View all" link | `Eksabli.Tenants.View` | navigates to `/admin/businesses` | no |
| View all tickets | "View all" link | `Eksabli.SupportTickets.Manage` | navigates to `/admin/support-tickets` | no |

### 10. States
Loading (skeleton tiles + skeleton list rows, matching `prototype/business/dashboard.html`'s existing
skeleton pattern) → Empty (0 pending approvals / 0 open tickets — show a positive "all caught up" message,
not a bare "0") → Error (if either widget's call fails, show that widget's own inline error, don't fail
the whole page) → Success.

### 11. Responsive Design
Stat tiles: 2-across desktop/tablet, stacked on mobile. List widgets: 2-column desktop, stacked mobile
(same pattern as `prototype/admin/dashboard.html`).

### 12. Dependencies
Businesses (02→03 navigation), Support Tickets (02→17 navigation).

### 13. Acceptance Criteria
- Admin sees a real total-business count and real pending-approval count, not placeholder numbers.
- Admin cannot see the Support Tickets widget without `SupportTickets.Manage`.
- No fabricated MRR/DAU/MAU/chart data is rendered — those widgets are either absent or explicitly
  marked as not-yet-available.
- Clicking "View all" on either widget navigates correctly.

---

# 03 — Businesses

**This page already exists** — `angular/src/app/admin/businesses/admin-tenants.component.{ts,html,scss}`,
build-verified. This section documents it as the reference pattern for the rest of this document, plus
the gaps found while re-examining it against this stricter template.

### 1. Page Overview
- **Page name:** Businesses (Tenants)
- **Route:** `/admin/businesses`
- **Purpose:** List every business (ABP Tenant) on the platform; approve pending signups; suspend active
  ones.
- **Primary user:** Super Admin, Content Moderator (approvals).
- **MVP or Future:** MVP — **built**.

### 2. User Journey
Admin lands from Dashboard or the sidebar menu (`Menu:AdminTenants`) → searches/filters by status →
scans the table → clicks Approve (Pending only) or Suspend (non-Suspended only) → confirms in a dialog →
toast confirms → list refreshes. Clicking a business name should navigate to Business Details (see §04) —
**this link does not exist yet in the built component** (see Risks).

### 3. Permissions
| Permission | Gates |
|---|---|
| `Eksabli.Tenants.View` | Route access, list read |
| `Eksabli.Tenants.Approve` | Approve action |
| `Eksabli.Tenants.Suspend` | Suspend action |

No `Create`/`Delete` — confirmed real: `AdminTenantsController` has no `POST`/`DELETE`. Businesses
self-register (via `BusinessController.RegisterBusinessAsync`, a tenant-onboarding flow, not an admin
action) — **there is intentionally no "New Business" button on this page.**

### 4. API
| Method | Endpoint | Purpose | Query params | Response | Permission |
|---|---|---|---|---|---|
| GET | `/api/app/admin-tenants` | List/search/filter | `approvalStatus`, `filterText`, `sorting`, `skipCount`, `maxResultCount` | `PagedResultDto<AdminTenantDto>` | `Tenants.View` |
| GET | `/api/app/admin-tenants/{tenantId}` | Single tenant | — | `AdminTenantDto` | `Tenants.View` |
| POST | `/api/app/admin-tenants/{tenantId}/approve` | Approve | — | `AdminTenantDto` | `Tenants.Approve` |
| POST | `/api/app/admin-tenants/{tenantId}/suspend` | Suspend | — | `AdminTenantDto` | `Tenants.Suspend` |

`AdminTenantDto` fields (confirmed): `tenantId`, `tenantName`, `businessProfileId`, `categoryId`,
`approvalStatus` (`TenantApprovalStatus`: 0=Pending, 1=Approved, 2=Suspended), `creationTime`.
⚠️ **No `plan`, `memberCount`, or `branchCount` field** — the prototype's Businesses table shows Plan and
Members columns; those are **not available from this endpoint**. 🔴 `[MISSING REQUIREMENT]` if those
columns are required — would need either a DTO extension server-side, or a second call per row (N+1,
not recommended) to `AdminSubscriptionsService` and some membership-count endpoint (which doesn't exist
either). **Current built component correctly omits these columns** rather than fabricating them.

### 5. UI Layout
Breadcrumb → search input + status filter (card) → table (Business, Status, Signed up, Actions) →
pagination footer. Matches `prototype/admin/businesses.html` minus the Plan/Members columns per the gap
above.

### 6. Components
`PageHeader`, `SearchInput`, `SelectFilter` (status), `DataTable`, `StatusBadge`, `Pagination`,
`ConfirmDialog` (via `@abp/ng.theme.shared`'s `ConfirmationService`, not a custom component — this is the
verified, correct pattern, confirmed against installed package source).
⚠️ **Currently all inline in one component**, not yet extracted into the shared components listed above —
see Shared Component Inventory at the end of this document for the extraction plan.

### 7. Table
| Column | Type | Sortable | Filterable | Searchable | Notes |
|---|---|---|---|---|---|
| Business | text | no (no `sorting` UI built, though API supports it) | no | yes (`filterText`) | Should link to Business Details — **missing today** |
| Status | badge | no | yes (status dropdown) | no | 3 states |
| Signed up | date | no | no | no | `creationTime` |
| Actions | buttons | — | — | — | Approve (Pending only), Suspend (not-Suspended only) |

Responsive: card-per-row collapse recommended below `sm` breakpoint (not yet built — current table is a
plain Bootstrap `<table>` with `.table-responsive` horizontal scroll, acceptable for MVP but not identical
to the prototype's mobile card treatment).

### 8. Forms
None on this page (approve/suspend are one-click actions, no form).

### 9. Actions
| Action | Button label | Permission | API | Confirm? | Success | Error |
|---|---|---|---|---|---|---|
| Approve | "Approve" | `Tenants.Approve` | `POST .../approve` | yes (`ConfirmationService.warn`) | "Business approved." | Generic ABP error toast (403/404 handled by global interceptor) |
| Suspend | "Suspend" | `Tenants.Suspend` | `POST .../suspend` | yes | "Business suspended." | same |

### 10. States
Loading (spinner row) ✅ built · Empty ("No businesses match your filters") ✅ built · Error (retry button)
✅ built · Success (list renders) ✅ built · Unauthorized/Forbidden — relies on `permissionGuard` redirecting
before the component mounts (not a component-level state) · Not Found — N/A for a list page · Saving —
buttons don't currently show a per-row spinner during approve/suspend (minor gap, low priority).

### 11. Responsive Design
Desktop: full table. Tablet: same, horizontal scroll if needed. Mobile: horizontal scroll (functional but
not ideal — a card-list mobile variant is a nice-to-have, not MVP-blocking).

### 12. Dependencies
Business Details (03→04, link not yet wired).

### 13. Acceptance Criteria
- ✅ Admin can search businesses by name.
- ✅ Admin can filter by status.
- 🔴 Admin can open business details — **not yet wired, needs Business Details page to exist first (§04)**.
- ✅ Admin cannot approve/suspend without the matching permission (guarded by `ConfirmationService` flow
  calling a permission-gated endpoint; a 403 will surface via the global HTTP error interceptor — verify
  the button itself is also hidden/disabled for users lacking the permission, which it currently is not
  — minor gap, add `*abpPermission` directive or manual `PermissionService` check).
- ✅ Pagination works correctly.
- ✅ API errors are displayed (load-failure state built and tested via `ng build`; runtime behavior not
  yet exercised against a live backend in this session).

---

# 04 — Business Details

### 1. Page Overview
- **Page name:** Business Details
- **Route:** `/admin/businesses/:tenantId`
- **Purpose:** Full picture of one business for support/oversight — profile, subscription, tickets.
- **Primary user:** Super Admin, Support Agent (view-as-tenant-adjacent use case, see Security note).
- **MVP or Future:** MVP for the Overview tab; Billing and Activity tabs are **partially blocked** by gaps
  below — see Dependencies.

### 2. User Journey
From Businesses list (§03), click a business name → lands here → reviews profile + status → optionally
approves/suspends from here too (same actions as the list, redundant but convenient) → switches tabs to
see subscription/tickets → uses browser back or breadcrumb to return to the list.

### 3. Permissions
| Permission | Gates |
|---|---|
| `Eksabli.Tenants.View` | Page access, Overview tab |
| `Eksabli.Tenants.Approve` / `.Suspend` | Same action buttons as the list page |
| `Eksabli.Billing.ManagePlatform` | Billing tab content |
| `Eksabli.SupportTickets.Manage` | Support Tickets tab content |

### 4. API
| Method | Endpoint | Purpose | Params | Response | Permission | Status |
|---|---|---|---|---|---|---|
| GET | `/api/app/admin-tenants/{tenantId}` | Overview tab | path: `tenantId` | `AdminTenantDto` | `Tenants.View` | ✅ Real |
| GET | `/api/app/support-ticket?tenantId={id}` | Tickets tab | query: `tenantId`, paging | `PagedResultDto<SupportTicketDto>` | `SupportTickets.Manage` | ✅ Real — `SupportTicketFilterDto.TenantId` confirmed to exist |
| GET | `/api/app/admin-subscriptions?...` | Billing tab | — | `PagedResultDto<TenantSubscriptionDto>` | `Billing.ManagePlatform` | ⚠️ Partial — **`AdminSubscriptionFilterDto` has NO `tenantId` field** (only `Status`). Cannot filter subscriptions to just this business server-side. |
| — | — | Billing tab, workaround | — | — | — | 🔴 `[MISSING REQUIREMENT]` — the only way to show "this business's subscription" today is to fetch the *entire* platform subscription list and filter client-side by `TenantSubscriptionDto.tenantId`, which doesn't scale and shouldn't ship. **Recommend**: add a `tenantId` filter to `AdminSubscriptionFilterDto` server-side before building this tab, or add a dedicated `GET /api/app/admin-subscriptions/by-tenant/{tenantId}` endpoint. |
| — | — | Activity/audit log tab | — | — | — | 🔴 `[MISSING REQUIREMENT]` — no audit log API at all (see §19). The prototype's "Activity Log" tab (view-as-tenant sessions, approval history) has no backing data source whatsoever. |

### 5. UI Layout
Breadcrumb (Admin Portal / Businesses / {name}) → left card (avatar/initials, name, category, status
badge, Approve/Suspend buttons) → right side: tabs (Overview, Billing, Support Tickets — **not** Activity
Log, per the gap above) → tab content.

### 6. Components
`PageHeader`, `Breadcrumb`, `DetailCard` (left profile card), `Tabs`, `StatusBadge`, `DataTable` (reused
for the Tickets tab), `EmptyState` (for Billing tab if the workaround above isn't implemented — show "not
available" rather than an empty table that looks broken).

### 7. Table
Support Tickets tab table: Subject, Priority, Status, Updated — reuses the same column shape as §17.

### 8. Forms
None (no edit form for tenant/business profile from the admin side — `BusinessProfile.Edit` permission
exists but is scoped to the **tenant's own** `BusinessProfileController`-equivalent, i.e. the business
edits its own profile; there's no admin-side "edit this business's profile on their behalf" endpoint).
🔴 `[MISSING REQUIREMENT]` if admin-side profile editing is actually wanted — not present today.

### 9. Actions
Same Approve/Suspend as §03, duplicated here for convenience (same permissions, same API, same
confirmation pattern).

### 10. States
Loading (skeleton profile card + skeleton tabs) → Not Found (invalid `tenantId` → 404 from
`GetAsync` — `AdminTenantsController` has no `[AllowAnonymous]`, confirms a real 404 body per the
verified ABP error-shape convention documented in `postman/gen/lib.js`) → Error → Success. Billing tab:
a distinct "not available" state (see §4) rather than a generic empty state, so it doesn't read as a bug.

### 11. Responsive Design
Desktop: 1/3 + 2/3 grid (profile card + tabs). Tablet: same, narrower. Mobile: stacked, profile card
first, tabs below.

### 12. Dependencies
- Businesses (parent list, §03)
- Subscriptions (§07, blocked per gap above)
- Support Tickets (§17, tenantId-filtered variant)
- Audit Logs (§19, blocked entirely)

### 13. Acceptance Criteria
- Admin can open a business's detail page from the list.
- Admin sees accurate profile/status data.
- Admin can approve/suspend from this page.
- 🔴 Admin canNOT currently see a real, server-filtered subscription for this business — must not fake
  this by fetching-all-and-filtering; ship the tab as "temporarily unavailable" or omit it until the
  backend gap is closed.
- 🔴 Activity Log tab must not be built until an audit log API exists.

---

# 05 — Users

### 1. Page Overview
- **Page name:** Users
- **Route:** 🧩 `/identity/users` (ABP's own route, already registered via
  `loadChildren: () => import('@abp/ng.identity').then(c => c.createRoutes())`)
- **Purpose:** Directory of Host-realm `IdentityUser` records.
- **Primary user:** Super Admin.
- **MVP or Future:** MVP, **but scope is much narrower than the docs describe** — see below.

### 2. User Journey
🧩 Entirely ABP stock behavior: navigate to Identity → Users, search/paginate the list, open a user to
edit roles/lock/unlock, etc.

### 3. Permissions
`[VERIFY — FRAMEWORK PERMISSION]` — ABP Identity module's own `AbpIdentity.Users` permission group
(`Default`/`Create`/`Update`/`Delete`/`ManagePermissions`), defined in the `Volo.Abp.Identity` package,
not in `EksabliPermissions.cs`. Confirm exact constant via
`Volo.Abp.Identity.IdentityPermissions.Users.*` at implementation time.

### 4. API
🧩 Stock ABP Identity REST API (`/api/identity/users`), already wired to the running host (the module is
referenced in both `EksabliDomainModule` and `EksabliHttpApiModule`, confirmed). No custom Eksabli
endpoint needed or should be written.

### 5–9. Layout / Components / Table / Forms / Actions
🧩 All provided by `@abp/ng.identity`'s `UsersComponent`. Do not rebuild this from scratch — importing and
routing to the module (already done in `app.routes.ts`) is the entire implementation task for a stock
version of this page.

### The real gap
🔴 `[MISSING REQUIREMENT]` — **this is the single biggest gap in the whole Admin Portal**, and it's a
product-design conflict, not just a missing endpoint. `docs/eksabli-loyalty-platform/06-dashboards-admin.md`
describes this page as: *"Search customers/staff across tenants (Host-level only)"* — a **cross-tenant**
search combining Host-realm customers AND every tenant's staff in one view. ABP's stock Identity Users
page does **not** do this: as a Host user, it shows Host-realm users only (which, per the two-realm
design, means **customers**, not tenant staff — tenant staff live under their own `TenantId`, invisible to
a Host-side Identity Users list without switching active tenant context first). There is no custom
"cross-tenant user directory" controller/app-service anywhere in `src/Eksabli.Application*`. Building the
docs' actual vision requires new backend work (a dedicated `AdminUserAppService` doing a cross-tenant
query, explicitly the kind of query the architecture docs call out as *only* legitimate from the Host
side — see `02-system-architecture.md`'s phone-lookup-scoping discussion for the general principle).
**Recommend for MVP:** ship the stock `@abp/ng.identity` Users page (Host-realm customers only, correctly
labeled "Customers", not "Users") and treat true cross-tenant staff search as a Future item requiring new
backend work.

### 10–13
Loading/empty/error states, responsive behavior, and acceptance criteria are all ABP Identity module
defaults — not this app's concern to redefine.

---

# 06 — User Details

### 1. Page Overview
- **Page name:** User Details
- **Route:** 🧩 No standalone route in stock ABP — the Users list opens a **drawer/modal** for
  create/edit, not a routed detail page. If a routed detail page (`/admin/users/:id`) is specifically
  wanted (matching this doc's requested page list), that's custom work layered on top of ABP's
  `IdentityUserService` (`GET /api/identity/users/{id}` — ✅ real, stock endpoint), not a missing backend
  capability, just missing UI.
- **Purpose:** Same as §05, one-record view.
- **MVP or Future:** Future, unless the modal-vs-route distinction matters to the product (it usually
  doesn't for an internal admin tool).

### 2–13
Same permission/API grounding as §05 (`Volo.Abp.Identity` stock module). The same cross-tenant limitation
applies: a "User Details" page cannot show a tenant staff member's `EmployeeAssignment` (branch, role)
data from the Host side without either (a) switching tenant context, which stock ABP supports via its own
tenant-switch mechanism, or (b) new backend work. 🔴 `[MISSING REQUIREMENT]` for any Eksabli-specific
fields (loyalty tier if the user is also a customer, employee role if staff) on top of the bare
`IdentityUserDto` — no endpoint joins `IdentityUser` with `CustomerProfile`/`EmployeeAssignment` from an
admin's cross-realm vantage point.

---

# 07 — Subscriptions

### 1. Page Overview
- **Page name:** Subscriptions
- **Route:** `/admin/subscriptions`
- **Purpose:** Every tenant's subscription status, platform-wide.
- **Primary user:** Billing/Finance Admin.
- **MVP or Future:** MVP.

### 2. User Journey
Admin navigates from sidebar → filters by status → scans list → (would click into a subscription for
detail, but see §08's gap) → cross-references with Businesses/Payments as needed.

### 3. Permissions
`Eksabli.Billing.ManagePlatform` gates the **entire** controller — there is no separate view-only
permission; anyone who can see this page can also, functionally, record manual payments (§4). If a
read-only "view but don't record payments" role is wanted, 🔴 `[MISSING REQUIREMENT]` — the backend
doesn't distinguish view from write here.

### 4. API
| Method | Endpoint | Purpose | Query params | Response | Permission |
|---|---|---|---|---|---|
| GET | `/api/app/admin-subscriptions` | List | `status` (`TenantSubscriptionStatus?`: 0=Trialing,1=Active,2=PastDue,3=Cancelled), `sorting`, `skipCount`, `maxResultCount` | `PagedResultDto<TenantSubscriptionDto>` | `Billing.ManagePlatform` |

`TenantSubscriptionDto` fields (confirmed): `id`, `tenantId`, `planId`, `planName`, `startDate`,
`renewalDate`, `status`. ⚠️ No tenant **name** on this DTO, only `tenantId` — the table will need to
resolve tenant names via a second lookup (e.g. cross-reference against a `AdminTenantsService.getList()`
call, or accept showing a raw GUID / add a name-resolution helper). Not a hard blocker, just a UI-layer
join to plan for.

### 5. UI Layout
Breadcrumb → stat row (Active count / Trialing count — both derivable client-side from the loaded page,
**not** a true platform-wide total unless `maxResultCount` is large enough or a second unpaginated count
call is made) → status filter → table (Business [needs name resolution], Plan, Status, Renewal date).

### 6. Components
`PageHeader`, `StatCard`, `SelectFilter`, `DataTable`, `StatusBadge`, `Pagination`.

### 7. Table
| Column | Type | Sortable | Filterable | Searchable | Notes |
|---|---|---|---|---|---|
| Business | text | no | no | 🔴 no search param on this endpoint at all | needs `tenantId → tenantName` resolution |
| Plan | text | no | no | no | `planName` |
| Status | badge | no | yes | no | 4 states |
| Renewal date | date | no | no | no | `renewalDate` |

🔴 `[MISSING REQUIREMENT]` — no `filterText`/search on `AdminSubscriptionFilterDto` at all (only
`Status`). The prototype's search box on this page has nothing to call.

### 8. Forms
None (no create/edit subscription form — subscriptions are created by the tenant's own billing lifecycle,
not admin-authored).

### 9. Actions
No approve/suspend-style actions on this page itself. "Record manual payment" lives conceptually here but
is really an Invoice-level action — see §11.

### 10. States
Standard loading/empty/error/success. Empty state copy should account for the no-search limitation
("No subscriptions match this status" rather than implying a broken search box).

### 11. Responsive Design
Standard table responsive pattern, same as §03.

### 12. Dependencies
Businesses (§03, for name resolution), Plans (§09, for plan context), Payments (§11).

### 13. Acceptance Criteria
- Admin can filter by subscription status.
- 🔴 Admin can NOT search by business name — not supported by the backend; do not build a search box that
  silently does nothing, either omit it or disable it with a tooltip explaining why.
- Table shows resolved business names, not raw tenant GUIDs.

---

# 08 — Subscription Details

### 1. Page Overview
- **Page name:** Subscription Details
- **Route:** `/admin/subscriptions/:id`
- **Purpose:** One subscription's full detail + its invoices.
- **MVP or Future:** 🔴 Blocked from being real MVP — see gap.

### 3–4. Permissions / API
🔴 `[MISSING REQUIREMENT]` — **`AdminSubscriptionsController` has no `GetAsync(id)` endpoint at all**,
only `GetListAsync`. There is no way to fetch a single subscription by its own id from the admin side.
The only viable path today: pass the already-loaded row from the Subscriptions list (§07) via router state
or an in-memory store, which is fragile (breaks on page refresh/deep link) and not a real "Details page"
in the normal sense.

The Invoices sub-list **is** independently fetchable and real:

| Method | Endpoint | Purpose | Query params | Response | Permission |
|---|---|---|---|---|---|
| GET | `/api/app/admin-subscriptions/invoices` | Invoices for this subscription | `tenantSubscriptionId` (✅ confirmed field on `AdminInvoiceFilterDto`), `status`, paging | `PagedResultDto<InvoiceDto>` | `Billing.ManagePlatform` |

### Recommendation
Do not build this as a fully route-addressable page for MVP. Either (a) fold subscription detail into a
drawer/expansion-row on the Subscriptions list (avoids the missing-GetAsync problem entirely since the
data's already in memory from the list call), or (b) add the missing `GetAsync(id)` to
`AdminSubscriptionsController` server-side first. Everything else in this section (layout, components,
states) is deferred pending that decision.

### 13. Acceptance Criteria
- Do not ship a page that 404s or shows stale/wrong data on refresh because it depended on router-passed
  state instead of a real fetch-by-id call.

---

# 09 — Plans

### 1. Page Overview
- **Page name:** Subscription Plans
- **Route:** `/admin/plans`
- **Purpose:** Manage the platform's plan catalog (Starter/Growth/Scale/Enterprise-style tiers).
- **Primary user:** Billing/Finance Admin, Super Admin.
- **MVP or Future:** MVP.

### 2. User Journey
Admin views the plan grid → creates a new plan or edits an existing one via a modal form → saves → toast
confirms → grid refreshes. Deletes a plan (with confirmation) if unused.

### 3. Permissions
| Permission | Gates |
|---|---|
| none (`[AllowAnonymous]`) | **Viewing** the list/detail — confirmed real: `GetListAsync`/`GetAsync` on `SubscriptionPlansController` are explicitly `[AllowAnonymous]`, commented in source as "public pricing catalog." This is intentional (prospective businesses see pricing pre-signup), not a bug — but it also means the Admin Portal page itself should still sit behind `authGuard` even though the API wouldn't stop an anonymous caller. |
| `Eksabli.Billing.ManagePlatform` | Create / Update / Delete |

### 4. API
| Method | Endpoint | Purpose | Params | Response | Permission |
|---|---|---|---|---|---|
| GET | `/api/app/subscription-plan` | List | `sorting`, `skipCount`, `maxResultCount` | `PagedResultDto<SubscriptionPlanDto>` | none |
| GET | `/api/app/subscription-plan/{id}` | Detail | path `id` | `SubscriptionPlanDto` | none |
| POST | `/api/app/subscription-plan` | Create | body `CreateUpdateSubscriptionPlanDto` | `SubscriptionPlanDto` | `Billing.ManagePlatform` |
| PUT | `/api/app/subscription-plan/{id}` | Update | path `id`, body `CreateUpdateSubscriptionPlanDto` | `SubscriptionPlanDto` | `Billing.ManagePlatform` |
| DELETE | `/api/app/subscription-plan/{id}` | Delete | path `id` | — | `Billing.ManagePlatform` |

`SubscriptionPlanDto`: `id`, `name`, `monthlyPrice` (decimal), `featureLimitsJson` (string — a raw JSON
blob, ⚠️ **not structured fields**; the prototype's "Branches / Members / Campaigns / Notifications"
columns are **encoded inside this one JSON string**, not separate DTO properties — the UI must parse
`featureLimitsJson` client-side to render those columns, and the Create/Edit form must serialize back into
it), `isTrialDefault` (bool).

### 5. UI Layout
Breadcrumb → "New Plan" button → responsive card grid (one card per plan, matches
`prototype/admin/plans.html`) → each card shows name, price, tenant count (⚠️ `[MISSING REQUIREMENT]` —
no "N tenants on this plan" count exists on this DTO; the prototype's number is decorative demo data and
has no real source) and parsed feature-limit bullets.

### 6. Components
`PageHeader`, `PlanCard` (new, domain-specific), `Modal` (create/edit form), `ConfirmDialog` (delete).

### 7. Table
N/A — card grid, not a table (matches prototype).

### 8. Forms
| Field | Type | Required | Validation | Default | API field | Error message |
|---|---|---|---|---|---|---|
| Name | text | yes | max `SubscriptionPlanConsts.MaxNameLength` (⚠️ exact number not read this session — check the consts file) | — | `name` | "Plan name is required." |
| Monthly price | number | no* | ≥ 0 | — | `monthlyPrice` | "Enter a price of 0 or more." *(`[Range]` validator allows 0; blank/Custom-pricing display, per Feature 04 docs, is a **UI convention**, not a nullable DTO field — `monthlyPrice` is a non-nullable `decimal`, so "blank = custom" must be represented as `0` or a sentinel value client-side, not literally omitted from the request. Confirm this convention before implementing — 🔴 `[MISSING REQUIREMENT]` for a proper nullable "custom pricing" flag.)* |
| Feature limits | structured sub-form (branches/members/campaigns/notifications limits) → serialized to `featureLimitsJson` | no | valid JSON on submit | `"{}"` | `featureLimitsJson` | "Feature limits couldn't be saved." |
| Is trial default | checkbox | no | — | `false` | `isTrialDefault` | — |

### 9. Actions
| Action | Button | Permission | API | Confirm? | Success | Error |
|---|---|---|---|---|---|---|
| Create | "New Plan" | `Billing.ManagePlatform` | POST | no | "Plan created." | validation errors inline |
| Edit | "Edit Plan" (per card) | `Billing.ManagePlatform` | PUT | no | "Plan saved." | inline |
| Delete | (not in prototype UI, but endpoint exists) | `Billing.ManagePlatform` | DELETE | yes | "Plan deleted." | e.g. "Cannot delete a plan with active subscriptions" if the backend enforces that (⚠️ not confirmed either way — verify server-side behavior before assuming delete is always safe) |

### 10. States
Standard loading/empty/error/success/saving/deleting.

### 11. Responsive Design
Card grid: 4-across desktop, 2-across tablet, 1-across mobile (matches prototype breakpoints).

### 12. Dependencies
Subscriptions (§07, plan name shown there), Business Details billing tab (§04, blocked).

### 13. Acceptance Criteria
- Admin can view plans without being logged in as platform staff specifically (technically true per the
  `[AllowAnonymous]` read) — but the **page itself** should still require `authGuard` since this is inside
  the Admin Portal shell.
- Admin cannot create/edit/delete without `Billing.ManagePlatform`.
- Feature limits round-trip correctly through the JSON-string field without data loss.

---

# 10 — Plan Details

### 1. Page Overview
- **Page name:** Plan Details
- **Route:** `/admin/plans/:id`
- **Purpose:** Single-plan read view (as distinct from the edit modal).
- **MVP or Future:** Future — **the "Edit Plan" modal on the Plans page (§09) already covers everything
  this route would show**, since `GetAsync(id)` and the edit form use the same `SubscriptionPlanDto`
  shape. Building a separate routed detail page is UI duplication, not a new capability. Recommend
  skipping this as its own page and instead deep-linking `/admin/plans?open={id}` to the same modal, or
  dropping it from scope entirely.

### 3–13
Same permissions/API as §09 (`GET /api/app/subscription-plan/{id}`, `[AllowAnonymous]`, ✅ real). No new
findings.

---

# 11 — Payments

### 1. Page Overview
- **Page name:** Payments
- **Route:** `/admin/payments`
- **Purpose:** Reconciliation view of platform billing transactions.
- **Primary user:** Billing/Finance Admin.
- **MVP or Future:** MVP, **but must be scoped as "Invoices," not "Payments"** — see gap.

### The core gap
🔴 `[MISSING REQUIREMENT]` — **there is no `Payment` read endpoint anywhere in this backend.** A
`Payment.cs` domain entity exists (with `Provider`, `ProviderTransactionRef`, `Status` per the database
design doc), and `IPaymentGateway`/`NullPaymentGateway` exist as an abstraction point, but **no controller
exposes `Payment` records for reading, and no refund endpoint exists at all.** The prototype's
`admin/payments.html` (Business / Amount / Provider / Reference / Status / Date / **Refund button**) has
**no real backend counterpart for the refund action** and its "Provider"/"Reference" columns describe
`Payment` fields specifically, not `Invoice` fields.

What **does** exist and is real:

| Method | Endpoint | Purpose | Query params | Response | Permission |
|---|---|---|---|---|---|
| GET | `/api/app/admin-subscriptions/invoices` | List invoices platform-wide | `status` (`InvoiceStatus?`: 0=Draft,1=Sent,2=Paid,3=Overdue), `tenantSubscriptionId`, paging | `PagedResultDto<InvoiceDto>` | `Billing.ManagePlatform` |
| POST | `/api/app/admin-subscriptions/record-manual-payment` | Mark an invoice paid manually (e.g. bank transfer reconciliation) | body: `invoiceId`, `providerTransactionRef?` | `InvoiceDto` | `Billing.ManagePlatform` |

`InvoiceDto`: `id`, `tenantSubscriptionId`, `amount`, `status`, `dueDate`, `paidAt`. No `tenantName`
(same join gap as §07), no provider/reference fields (those belong to `Payment`, not `Invoice`, and
`Payment` isn't exposed).

### Recommendation
Rename this page's real scope to **Invoices** (or clearly subtitle "Payments (via Invoices)" so it's
honest about what data backs it). Build: list + status filter + "Record Manual Payment" action (the one
real write capability). **Do not build a Refund button** — there is nothing for it to call. If refunds are
a genuine near-term need, flag it back to the product/backend team; this doc will not invent a refund
endpoint to make the UI feel complete.

### 5–10 (abbreviated per the gap above)
Table: Business (needs resolution same as §07), Amount, Status, Due date, Paid date, Action ("Record
Payment" for non-Paid invoices only). Loading/empty/error/success standard. Form for Record Manual
Payment: `invoiceId` (implicit from row context), `providerTransactionRef` (optional text).

### 13. Acceptance Criteria
- Admin can view invoices and filter by status.
- Admin can record a manual payment against an unpaid invoice.
- 🔴 No refund UI ships without a corresponding backend endpoint.
- Page/menu copy does not overclaim "Payments" if what's shown is invoices — avoid a support ticket from
  an admin confused about a missing Provider column.

---

# 12 — Payment Details

### 1. Page Overview
🔴 `[MISSING REQUIREMENT]` in full — there is no `Payment` entity endpoint (see §11), so there is nothing
to build a "Payment Details" page against. If "Payment Details" really means "Invoice Details," that's
just the same `InvoiceDto` already loaded in the §11 list (no separate `GetAsync(id)` exists on invoices
either, same missing-detail-endpoint pattern as §08). **Recommend dropping this page from MVP scope
entirely** until either a real `Payment` read endpoint or an `Invoice` `GetAsync(id)` endpoint exists.

---

# 13 — Categories

### 1. Page Overview
- **Page name:** Categories
- **Route:** `/admin/categories`
- **Purpose:** Manage the business category taxonomy used by discovery/search.
- **Primary user:** Super Admin, Content Moderator.
- **MVP or Future:** MVP.

### 2. User Journey
Admin views the category table → creates/edits via modal (bilingual name fields) → deletes with
confirmation (warned that businesses in a deleted category need recategorizing, matching the prototype's
existing confirmation copy).

### 3. Permissions
| Permission | Gates |
|---|---|
| none | View (list/get are `[AllowAnonymous]`, same public-taxonomy rationale as Plans) |
| `Eksabli.Categories.Create` | Create |
| `Eksabli.Categories.Edit` | Update |
| `Eksabli.Categories.Delete` | Delete |

### 4. API
| Method | Endpoint | Purpose | Params | Response | Permission |
|---|---|---|---|---|---|
| GET | `/api/app/category` | List | `parentCategoryId?`, `filterText?`, paging | `PagedResultDto<CategoryDto>` | none |
| GET | `/api/app/category/{id}` | Detail | path `id` | `CategoryDto` | none |
| POST | `/api/app/category` | Create | body `CreateUpdateCategoryDto` | `CategoryDto` | `Categories.Create` |
| PUT | `/api/app/category/{id}` | Update | path `id`, body `CreateUpdateCategoryDto` | `CategoryDto` | `Categories.Edit` |
| DELETE | `/api/app/category/{id}` | Delete | path `id` | — | `Categories.Delete` |

`CategoryDto`: `id`, `nameAr`, `nameEn`, `iconBlobName?`, `parentCategoryId?`. ⚠️ No "business count in
this category" field — the prototype's Businesses column is 🔴 `[MISSING REQUIREMENT]` for the same
reason as §09's tenant-count column (would need an `AdminTenantsController` category filter that doesn't
exist, or a new aggregate endpoint).

### 5. UI Layout
Breadcrumb → "New Category" button → table (Category [both languages? or active-locale only — **design
decision**, not a backend gap], Actions [edit/delete icon buttons]).

### 6. Components
`PageHeader`, `DataTable`, `Modal` (create/edit, bilingual name fields), `ConfirmDialog`.

### 7. Table
| Column | Type | Sortable | Filterable | Searchable | Notes |
|---|---|---|---|---|---|
| Category | text | no | by `parentCategoryId` (subcategory filter) | yes (`filterText`) | show `nameEn`/`nameAr` per active locale |
| Actions | icon buttons | — | — | — | Edit, Delete |

### 8. Forms
| Field | Type | Required | Validation | Default | API field | Error message |
|---|---|---|---|---|---|---|
| Name (Arabic) | text | yes | max `CategoryConsts.MaxNameLength` (⚠️ exact number not read this session) | — | `nameAr` | "Arabic name is required." |
| Name (English) | text | yes | same | — | `nameEn` | "English name is required." |
| Icon | file/blob picker | no | max `CategoryConsts.MaxIconBlobNameLength` | — | `iconBlobName` | — |
| Parent category | select | no | must reference an existing category | none (top-level) | `parentCategoryId` | — |

### 9. Actions
| Action | Button | Permission | API | Confirm? | Success | Error |
|---|---|---|---|---|---|---|
| Create | "New Category" | `Categories.Create` | POST | no | "Category saved." | inline validation |
| Edit | pencil icon | `Categories.Edit` | PUT | no | "Category saved." | inline |
| Delete | trash icon | `Categories.Delete` | DELETE | yes ("Businesses currently in this category will need to be recategorized.") | "Category deleted." | e.g. FK-constraint error if backend blocks deleting a category in use — ⚠️ not confirmed either way |

### 10. States
Standard loading/empty/error/success/saving/deleting.

### 11. Responsive Design
Standard table pattern; icon picker may need a simplified mobile variant (lower priority).

### 12. Dependencies
Businesses (§03, category shown per tenant via `AdminTenantDto.categoryId` — name resolution needed
there too).

### 13. Acceptance Criteria
- Admin can create a category with both Arabic and English names.
- Admin cannot create/edit/delete without the matching permission.
- Deleting a category prompts the recategorization warning.

---

# 14 — Campaigns

### 1. Page Overview
🔴 `[MISSING REQUIREMENT]` — **architecturally wrong realm, not just a missing field.** The only
`CampaignsController` in this codebase is `[Authorize(EksabliPermissions.Campaigns.Default)]`, and
`Campaigns.Default`/`.Create`/`.Edit`/`.Activate` are defined in the **tenant-realm** block of
`EksabliPermissions.cs` (alongside `Rewards`, `Offers`, `Branches` — all business-self-service
permissions), not the Host-realm block (`Tenants`/`Categories`/`SupportTickets`). Every call implicitly
scopes to `CurrentTenant.Id` — there is no `tenantId` parameter to target a specific business's campaigns
from the Host side, and there is no concept of a **platform-wide announcement campaign** (what
`06-dashboards-admin.md` actually describes for this Admin Portal page: *"Platform-wide announcements
(not tenant campaigns) — e.g. 'New feature' banners"*) anywhere in the domain model.

**This page belongs to the Business Portal (Feature 05), not the Admin Portal.** Recommend removing it
from the Admin Portal's scope entirely, or — if platform-wide announcements are a real product need —
flagging it to the backend team as genuinely new work (a new `PlatformAnnouncement` concept), not
something this plan can spec against existing endpoints without inventing them.

### 13. Acceptance Criteria
- N/A until the backend gap is resolved or the page is descoped.

---

# 15 — Campaign Details

Same finding as §14 — 🔴 `[MISSING REQUIREMENT]`, same reasoning, not repeated.

---

# 16 — Reports

### 1. Page Overview
🔴 `[MISSING REQUIREMENT]` — same shape of problem as Campaigns. `ReportsController` is
`[Authorize(EksabliPermissions.Reports.Default)]`, and `Reports.Default`/`.Export` live in the
**tenant-realm** permission block. Every endpoint on it (`dashboard-home`, `member-growth`,
`redemption-rate`, `branch-comparison`, `customer-segments`, `tier-distribution`, `top-customers`,
`campaign-performance`, `notification-delivery-rates`, `transactions` export) is implicitly scoped to
`CurrentTenant.Id` — **this is the Business Portal's Analytics/Reports data (Feature 07), not
platform-wide data.** `06-dashboards-admin.md`'s Admin Panel "Platform Reports" (tenant growth, DAU/MAU,
category mix, support ticket volume/resolution time) has **no corresponding backend endpoint anywhere**.

### Recommendation
Do not build an Admin "Reports" page against `ReportsController` — that would silently show one
business's data mislabeled as platform-wide, which is worse than not building the page at all. The only
genuinely platform-wide, real numbers available today are the same ones already covered in §02 Dashboard
(business counts via `AdminTenantsService`, ticket counts via `SupportTicketsService`) — if a dedicated
Reports page is wanted for MVP, its real scope is "a slightly bigger version of the Dashboard," not the
rich analytics suite the docs describe. Flag the gap back to product/backend rather than building against
the wrong-realm endpoint.

### 13. Acceptance Criteria
- N/A until either new backend endpoints exist or the page's scope is reduced to what §02 already covers.

---

# 17 — Support Tickets

### 1. Page Overview
- **Page name:** Support Tickets
- **Route:** `/admin/support-tickets`
- **Purpose:** Queue of every support ticket (customer + business), for Support Agent triage.
- **Primary user:** Support Agent.
- **MVP or Future:** MVP.

### 2. User Journey
Admin views the queue, filtered by status/priority tabs (matching the prototype's Open/Pending/Resolved
tab pattern, though see status enum note below) → opens a ticket → reads the thread → replies → resolves.

### 3. Permissions
| Permission | Gates |
|---|---|
| `Eksabli.SupportTickets.Manage` | The full queue (`GetListAsync`), resolving |
| none (just authenticated) | Creating a ticket, getting/replying to **your own** ticket — not relevant to the Admin Portal's queue view, but shared by the same controller |

### 4. API
| Method | Endpoint | Purpose | Params | Response | Permission |
|---|---|---|---|---|---|
| GET | `/api/app/support-ticket` | Queue | `status?`, `priority?`, `tenantId?`, paging | `PagedResultDto<SupportTicketDto>` (list items have empty `messages`) | `SupportTickets.Manage` |
| GET | `/api/app/support-ticket/{id}` | Thread detail | path `id` | `SupportTicketDto` (with populated `messages`) | authenticated + ownership OR `Manage` |
| POST | `/api/app/support-ticket/{id}/messages` | Reply | path `id`, body `AddSupportTicketMessageDto` | `SupportTicketMessageDto` | authenticated + ownership OR `Manage` |
| POST | `/api/app/support-ticket/{id}/resolve` | Resolve | path `id` | `SupportTicketDto` | `SupportTickets.Manage` |

`SupportTicketStatus` enum (confirmed via `postman/gen/enums.js`): `Open`, `InProgress`, `Resolved`,
`Closed` (4 values). ⚠️ **Only a `resolve` action exists** — no endpoint to transition to `InProgress` or
`Closed` explicitly, and no "reopen" endpoint. 🔴 `[MISSING REQUIREMENT]` if the UI wants to show all 4
statuses as admin-actionable (the prototype's 3-tab Open/Pending/Resolved layout doesn't even map cleanly
onto the real 4-value enum — "Pending" isn't a real status value; nearest real equivalent is
`InProgress`).

`SupportTicketPriority` enum: `Low`, `Medium`, `High`, `Urgent`.

### 5. UI Layout
Breadcrumb → status tabs (recommend: Open / In Progress / Resolved / Closed, matching the real enum, not
the prototype's invented "Pending" label) → ticket cards/rows (Subject, From, Type [Business/Customer,
derived client-side from whether `tenantId` or `customerId` is set], Priority badge, Updated) → click
opens thread drawer/page.

### 6. Components
`PageHeader`, `Tabs`, `TicketListItem` (or reuse `DataTable`), `PriorityBadge`, `StatusBadge`,
`ThreadDrawer` (message list + reply textarea), `Pagination`.

### 7. Table
| Column | Type | Sortable | Filterable | Searchable | Notes |
|---|---|---|---|---|---|
| Subject | text | no | no | 🔴 no `filterText` on `SupportTicketFilterDto` — only `status`/`priority`/`tenantId` | |
| From | derived | no | via `tenantId` filter (business-only view) | — | no direct "customer name" filter either — filter is by `tenantId` GUID, not free text |
| Priority | badge | no | yes | no | |
| Status | badge | no | yes | no | |
| Updated | date | no | no | no | uses `AuditedEntityDto`'s `lastModificationTime`/`creationTime` |

### 8. Forms
Reply form: `body` (textarea, required, no documented max length — check `AddSupportTicketMessageDto`
constraints at implementation time, not read this session).

### 9. Actions
| Action | Button | Permission | API | Confirm? | Success | Error |
|---|---|---|---|---|---|---|
| Reply | "Send Reply" | Manage (or ownership) | POST messages | no | "Reply sent." | inline |
| Resolve | "Mark Resolved" | `SupportTickets.Manage` | POST resolve | recommend yes (irreversible-ish without a reopen endpoint) | "Ticket marked resolved." | e.g. "Already closed" if backend guards double-resolve |

### 10. States
Standard loading/empty/error/success. Special case: replying to an already-resolved/closed ticket should
be disabled client-side (no reopen path exists) with a clear "This ticket is closed" message — this
matches a real backend business rule already localized in `en.json`/`ar.json`
(`AdminPanel:TicketAlreadyClosedMessage`, confirmed present).

### 11. Responsive Design
Desktop: list + slide-over drawer for thread. Mobile: full-screen thread view instead of a drawer.

### 12. Dependencies
Business Details (§04, tenant-filtered ticket view), Dashboard (§02, recent-tickets widget).

### 13. Acceptance Criteria
- Admin can view the full ticket queue (not just their own tickets).
- Admin can filter by status and priority.
- Admin can reply and resolve.
- Admin cannot reply to a closed ticket (matches existing localized error message).
- 🔴 No "reopen" or granular status-transition UI ships without the corresponding endpoint.

---

# 18 — Support Ticket Details

Covered together with §17 above (same controller, `GetAsync(id)` for the thread view) — this repo's
natural pattern is a drawer/panel rather than a separate route (see prototype's existing drawer pattern in
`admin/support-tickets.html`), but a routed `/admin/support-tickets/:id` is equally supportable since
`GetAsync(id)` is real. No additional gaps beyond §17's.

---

# 19 — Audit Logs

### 1. Page Overview
🔴 `[MISSING REQUIREMENT]` — **confirmed, hard blocker, backend change required before any UI work.**
`AbpAuditLoggingDomainModule` **is** referenced in `EksabliDomainModule.cs` (so audit data is actively
being recorded to the database right now), but `AbpAuditLoggingHttpApiModule` is **not** referenced in
`EksabliHttpApiModule.cs` (confirmed by reading the file directly — only Permission/Setting/Identity/
Tenant/Feature Management HttpApi modules are listed). **There is no REST endpoint to read audit log data
today.** Additionally, no `@abp/ng.audit-logging` package is installed on the Angular side.

### Recommendation
This page cannot be implemented as real, working software right now. Two backend prerequisites, in order:
1. Add `typeof(AbpAuditLoggingHttpApiModule)` to `EksabliHttpApiModule`'s `[DependsOn]` list (small,
   mechanical change — exposes ABP's standard `/api/audit-logging/audit-logs` endpoint).
2. `npm install @abp/ng.audit-logging` in `angular/` to get the ready-made list component, or build
   custom against the now-exposed REST API.

Until #1 lands, this page should not be scheduled into an Angular sprint — there's nothing to build
against. Flag to whoever owns backend prioritization.

### 13. Acceptance Criteria
- N/A until the backend prerequisite is met.

---

# 20 — Feature Flags

### 1. Page Overview
- **Page name:** Feature Flags
- **Route:** `/admin/feature-flags` (custom-styled) or reuse `@abp/ng.feature-management`'s own UI
  surface directly (**design decision** — see below).
- **Purpose:** Thin UI over ABP's Feature Management module — plan entitlements + platform rollout flags,
  exactly as `docs/.../08-admin-panel/README.md` specifies ("this feature just puts a thin UI over
  [ABP modules], it doesn't reimplement them").
- **Primary user:** Super Admin.
- **MVP or Future:** MVP.

### 3. Permissions
`[VERIFY — FRAMEWORK PERMISSION]` — ABP's own `FeatureManagement.ManageHostFeatures`-shaped permission
(exact constant in `Volo.Abp.FeatureManagement.Permissions.FeatureManagementPermissions` — package is
installed and its HttpApi module **is** referenced, unlike Audit Logging, confirmed real).

### 4. API
🧩 Stock ABP Feature Management REST API (`/api/feature-management/features?providerName=...`) — real,
confirmed referenced in both Domain and HttpApi modules.

### 5–9
🧩 `@abp/ng.feature-management` (installed) ships a ready `FeatureManagementComponent`, normally invoked
as a modal against a specific provider (e.g. a tenant, or `"H"` for host-level default features). Building
a **standalone page** listing all feature definitions independent of a provider context is not the stock
module's default UX pattern — it's typically "manage features for X," not "browse all feature flags."
⚠️ If a flat, provider-independent flag list (matching `prototype/admin/feature-flags.html`'s simple
toggle-switch list) is wanted, that's custom UI work against the same real API, not a missing endpoint.

### 10–13
Standard states. Acceptance criteria: admin can toggle a host-level feature; the change persists (verify
via a page reload, not just an optimistic UI update).

---

# 21 — Settings

### 1. Page Overview
- **Page name:** System Settings
- **Route:** 🧩 `/setting-management` — **already registered** in `app.routes.ts`
  (`loadChildren: () => import('@abp/ng.setting-management').then(c => c.createRoutes())`).
- **Purpose:** Global platform configuration.
- **MVP or Future:** MVP — **arguably already "done"** in the sense that the route exists; the remaining
  work is (a) confirming a menu entry links to it (not yet added to `route.provider.ts`, unlike
  `/admin/businesses`), and (b) deciding whether the stock ABP settings tabs are sufficient or a custom
  page (matching the prototype's Notification Providers / Platform Defaults cards) is wanted.

### 3–4. Permissions / API
🧩 Stock `Volo.Abp.SettingManagement` — HttpApi module confirmed referenced. Permission
`[VERIFY — FRAMEWORK PERMISSION]` per `SettingManagementPermissions`.

### The gap
🔴 `[MISSING REQUIREMENT]` for the prototype's specific fields (notification provider API keys — Firebase
Server Key, Email API Key, SMS Aggregator key; default plan for new signups; trial length; maintenance
mode toggle). ABP's stock Setting Management UI manages **generic key/value settings already defined via
`ISettingDefinitionProvider`** — none of these Eksabli-specific settings have been defined as actual ABP
`SettingDefinition`s anywhere in `src/Eksabli.Domain/Settings/` (confirmed the folder exists but wasn't
read in depth this session — recommend a follow-up check of exactly what's defined there before assuming
these prototype fields are wireable at all).

### 13. Acceptance Criteria
- Admin can reach Settings from the sidebar menu (needs a `route.provider.ts` entry, currently missing).
- Whatever settings **are** defined via `ISettingDefinitionProvider` are editable and persist.
- Prototype-specific fields not backed by a real `SettingDefinition` are not built until confirmed to
  exist.

---

# 22 — Roles

### 1. Page Overview
- **Page name:** Roles
- **Route:** 🧩 Within `/identity` (ABP Identity module's own Roles tab — already registered).
- **Purpose:** Manage Host-realm roles (Super Admin, Support Agent, Billing Admin, Content Moderator per
  the business-strategy doc's role table).
- **MVP or Future:** MVP.

### 3–13
🧩 Entirely stock `@abp/ng.identity`. `[VERIFY — FRAMEWORK PERMISSION]` for
`IdentityPermissions.Roles.*`. The four Host-realm roles named in `01-business-strategy.md` need to
actually exist as seeded `IdentityRole` rows — 🔴 `[MISSING REQUIREMENT]` to confirm: not verified this
session whether a data seeder creates these four roles, or whether they'd need to be created manually via
this very page on first use.

---

# 23 — Role Details

Same as §22 — stock ABP Identity, modal/drawer pattern (not a separate route by default), includes the
per-role **Permissions** grant screen (`@abp/ng.permission-management`, confirmed installed transitively)
launched from here. This is where the standalone §24 concept actually lives in stock ABP.

---

# 24 — Permissions

### 1. Page Overview
🔴 `[MISSING REQUIREMENT]` **as a standalone page**, with an important nuance: the *capability* exists
(`@abp/ng.permission-management` is installed and its HttpApi module is referenced), but only in ABP's
stock shape — a **modal bound to one specific role or user** ("edit permissions for Role X"), opened from
§22/§23. There is no stock "browse the full `EksabliPermissions` catalog independent of any role" page.
Given `EksabliPermissions.cs` has 14 permission groups (`BusinessProfile`, `Branches`,
`EmployeeAssignments`, `Memberships`, `Tiers`, `PointRules`, `Rewards`, `Billing`, `Campaigns`, `Offers`,
`Notifications`, `Achievements`, `Followers`, `Reports`, `Tenants`, `Categories`, `SupportTickets` — 17,
actually), a genuinely useful standalone "Permissions" page (a reference/audit view of what every
permission does and which roles have it) would be **custom-built read-only UI** — no single endpoint
returns "all permissions × all roles" as a matrix; it would require iterating roles and calling the
permission-management API once per role. Recommend scoping this as Future, and for MVP relying entirely on
the per-role modal reached from §22/§23.

---

# 25 — Notifications

### 1. Page Overview
🔴 `[MISSING REQUIREMENT]` — genuinely ambiguous, not just missing. The only `NotificationsController` in
this codebase is `[Authorize(EksabliPermissions.Notifications.Send)]`, and `Notifications.Send` is a
**tenant-realm** permission (marketing staff sending to their own business's customers) — same wrong-realm
pattern as Campaigns/Reports. There is no Host-realm concept of "admin's own notification inbox,"
"platform-wide announcements," or "system alerts" anywhere in the domain model. Before this page can be
specced meaningfully, product needs to answer: is this (a) an admin-facing inbox of internal system
alerts (doesn't exist, would be new scope), (b) a way for platform admins to broadcast to all businesses
(doesn't exist, overlaps with the Campaigns gap in §14), or (c) something else? **Do not build against the
tenant `NotificationsController` — it's the wrong permission model for a Host-realm user.**

### 13. Acceptance Criteria
- N/A pending a product decision on what this page is actually for.

---

# 26 — Admin Profile

### 1. Page Overview
- **Page name:** Admin Profile / Account Settings
- **Route:** 🧩 `/account/manage` (ABP Account module, already registered under `/account`)
- **Purpose:** Self-service profile + password management for the logged-in Host-realm staff member.
- **Primary user:** Any Host-realm user, for themselves.
- **MVP or Future:** MVP.

### 3–4. Permissions / API
None beyond authentication — same generic ABP Identity self-service endpoints already **verified live**
in `postman/gen/mobile.js`'s Profile folder (these are realm-agnostic, apply equally to a Host admin):

| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/api/account/my-profile` | Load profile |
| PUT | `/api/account/my-profile` | Update profile (⚠️ verified live behavior: passing `null` for `concurrencyStamp` skips the optimistic-concurrency check — see Mobile API collection notes) |
| POST | `/api/account/my-profile/change-password` | Change password (⚠️ verified live: wrong current password returns **403**, not 400) |

### 5–13
🧩 Entirely `@abp/ng.account`'s stock `ManageProfileComponent`, already wired via the existing
`loadChildren` import for `/account`. No custom work needed beyond confirming a link exists in the topbar
user-menu (not yet added — the built `admin-tenants.component`'s page doesn't have a topbar/user-menu at
all yet, since no shell/layout component has been built beyond ABP's own `DynamicLayoutComponent`).

---
---

# Cross-Cutting Sections

## 1. Complete Page Inventory

| # | Page | Route | MVP/Future | Backend status |
|---|---|---|---|---|
| 01 | Login | `/account/login` | MVP | ✅ Real (stock ABP) |
| 02 | Dashboard | `/admin` | MVP (reduced scope) | ⚠️ Partial |
| 03 | Businesses | `/admin/businesses` | MVP | ✅ Real — **built** |
| 04 | Business Details | `/admin/businesses/:id` | MVP (Overview only) | ⚠️ Partial |
| 05 | Users | `/identity/users` | MVP (as "Customers," Host-realm only) | ⚠️ Partial (stock ABP, wrong cross-tenant scope vs. docs) |
| 06 | User Details | (modal, not routed) | Future | ⚠️ Partial |
| 07 | Subscriptions | `/admin/subscriptions` | MVP | ⚠️ Partial (no search) |
| 08 | Subscription Details | `/admin/subscriptions/:id` | Future (blocked) | 🔴 Missing `GetAsync` |
| 09 | Plans | `/admin/plans` | MVP | ✅ Real |
| 10 | Plan Details | `/admin/plans/:id` | Future (redundant w/ §09 modal) | ✅ Real but low value |
| 11 | Payments | `/admin/payments` | MVP (rescoped to Invoices) | ⚠️ Partial |
| 12 | Payment Details | `/admin/payments/:id` | Future (blocked) | 🔴 No endpoint |
| 13 | Categories | `/admin/categories` | MVP | ✅ Real |
| 14 | Campaigns | `/admin/campaigns` | **Descope** | 🔴 Wrong realm |
| 15 | Campaign Details | `/admin/campaigns/:id` | **Descope** | 🔴 Wrong realm |
| 16 | Reports | `/admin/reports` | **Descope** | 🔴 Wrong realm |
| 17 | Support Tickets | `/admin/support-tickets` | MVP | ✅ Real |
| 18 | Support Ticket Details | `/admin/support-tickets/:id` | MVP | ✅ Real |
| 19 | Audit Logs | `/admin/audit-logs` | Future (backend blocker) | 🔴 No REST API |
| 20 | Feature Flags | `/admin/feature-flags` | MVP | ✅ Real (stock ABP) |
| 21 | Settings | `/setting-management` | MVP (generic settings only) | ⚠️ Partial |
| 22 | Roles | `/identity/roles` | MVP | ✅ Real (stock ABP) |
| 23 | Role Details | (modal, not routed) | MVP | ✅ Real (stock ABP) |
| 24 | Permissions | (modal from §22/23) | Future (standalone) | ⚠️ Partial (modal only) |
| 25 | Notifications | — | **Descope pending product decision** | 🔴 No concept exists |
| 26 | Admin Profile | `/account/manage` | MVP | ✅ Real (stock ABP) |

## 2. API Dependency Matrix

| Page | Controller(s) used |
|---|---|
| Dashboard | `AdminTenantsController`, `SupportTicketsController` |
| Businesses / Business Details | `AdminTenantsController` |
| Business Details (Billing tab) | `AdminSubscriptionsController` ⚠️ |
| Business Details (Tickets tab) | `SupportTicketsController` |
| Users / User Details | ABP `IdentityController` (stock) |
| Subscriptions / Subscription Details | `AdminSubscriptionsController` |
| Plans / Plan Details | `SubscriptionPlansController` |
| Payments / Payment Details | `AdminSubscriptionsController` (invoices endpoints) |
| Categories | `CategoriesController` |
| Support Tickets / Details | `SupportTicketsController` |
| Feature Flags | ABP `FeatureManagementController` (stock) |
| Settings | ABP `SettingManagementController` (stock) |
| Roles / Role Details / Permissions | ABP `IdentityRoleController` + `PermissionManagementController` (stock) |
| Admin Profile | ABP `AccountController` (stock) |
| Audit Logs | 🔴 none — no controller exposed yet |
| Campaigns / Campaign Details / Reports / Notifications | 🔴 none usable — controllers exist but wrong realm |

## 3. Permission Matrix

| Permission | Pages that use it |
|---|---|
| `Eksabli.Tenants.View` | Dashboard, Businesses, Business Details |
| `Eksabli.Tenants.Approve` | Businesses, Business Details |
| `Eksabli.Tenants.Suspend` | Businesses, Business Details |
| `Eksabli.Billing.ManagePlatform` | Subscriptions, Subscription Details, Plans, Plan Details, Payments, Payment Details, Business Details (Billing tab) |
| `Eksabli.Categories.Create/.Edit/.Delete` | Categories |
| `Eksabli.SupportTickets.Manage` | Dashboard, Support Tickets, Support Ticket Details, Business Details (Tickets tab) |
| `[VERIFY]` `IdentityPermissions.Users.*` | Users, User Details |
| `[VERIFY]` `IdentityPermissions.Roles.*` | Roles, Role Details |
| `[VERIFY]` `PermissionManagementPermissions.*` | Permissions (modal) |
| `[VERIFY]` `FeatureManagementPermissions.*` | Feature Flags |
| `[VERIFY]` `SettingManagementPermissions.*` | Settings |
| none (public/anonymous read) | Plans (read), Categories (read) |
| 🔴 none exists | Campaigns, Reports, Audit Logs, Notifications |

## 4. Shared Component Inventory

| Component | Used by | Status |
|---|---|---|
| `PageHeader` (title + subtitle + breadcrumb slot) | every page | 🔴 not yet extracted — currently inlined in `admin-tenants.component.html` |
| `Breadcrumb` | every page | 🔴 not yet extracted |
| `SearchInput` (debounced) | Businesses, Categories | 🔴 not yet extracted — debounce logic currently duplicated inline |
| `SelectFilter` | Businesses, Subscriptions, Support Tickets | 🔴 not yet extracted |
| `DataTable` (generic, with loading/empty/error row states baked in) | Businesses, Subscriptions, Payments, Categories, Support Tickets | 🔴 not yet extracted — the loading/empty/error `@if` chain is currently duplicated verbatim in `admin-tenants.component.html`; **highest-value extraction target** since every list page repeats it |
| `StatusBadge` | Businesses, Subscriptions, Payments, Support Tickets | 🔴 not yet extracted — `statusBadgeClass()`/`statusLabelKey()` pattern currently lives inside the component class |
| `PriorityBadge` | Support Tickets | new |
| `Pagination` | every list page | 🔴 not yet extracted — currently a hand-rolled prev/next in `admin-tenants.component.html`, not a real page-number pagination control |
| `StatCard` | Dashboard, Subscriptions | new |
| `DataListWidget` | Dashboard | new |
| `PlanCard` | Plans | new |
| `ConfirmDialog` | Businesses, Categories, Support Tickets, Plans | 🧩 not a custom component — `ConfirmationService` (verified real, `@abp/ng.theme.shared`) |
| `ThreadDrawer` | Support Ticket Details | new |
| `EmptyState` | most pages | 🔴 not yet extracted — currently just an inline `<tr>` |

**Recommendation:** given Businesses (§03) is the only built page, extract `DataTable` + `Pagination` +
`StatusBadge` + `SearchInput` as the very next step, refactor Businesses to use them, and every subsequent
list page (Subscriptions, Payments, Categories, Support Tickets) becomes materially less code from day
one instead of copy-pasting `admin-tenants.component`'s inline patterns four more times.

## 5. Shared Service Inventory

| Service | Source | Status |
|---|---|---|
| `AdminTenantsService` | generated proxy | ✅ used |
| `AdminSubscriptionsService` | generated proxy | not yet used |
| `SubscriptionPlansService` | generated proxy | not yet used |
| `CategoriesService` | generated proxy | not yet used |
| `SupportTicketsService` | generated proxy | not yet used |
| `ConfirmationService`, `ToasterService` | `@abp/ng.theme.shared` | ✅ used |
| `PermissionService` | `@abp/ng.core` | not yet used — **needed** to hide/disable action buttons per-permission (currently only the *route* is permission-guarded on Businesses, not individual buttons within it) |
| A tenant-name-resolution helper/cache | 🔴 doesn't exist | **needed** — Subscriptions, Payments, and (arguably) Categories all need to resolve a bare `tenantId` GUID to a display name; building this once (e.g. a small `AdminTenantLookupService` wrapping `AdminTenantsService` with an in-memory cache) avoids repeating an N+1 pattern four times |

## 6. Route Map

```
/account/login                    (01, stock ABP, empty layout)
/account/manage                   (26, stock ABP)
/home                             (existing placeholder, unrelated to admin scope)
/admin                            (02 Dashboard)
/admin/businesses                 (03, BUILT)
/admin/businesses/:tenantId       (04)
/admin/subscriptions              (07)
/admin/subscriptions/:id          (08 — do not build until GetAsync exists; consider drawer instead)
/admin/plans                      (09)
/admin/plans/:id                  (10 — low priority, redundant with 09's modal)
/admin/payments                   (11, rescoped to Invoices)
/admin/categories                 (13)
/admin/support-tickets            (17)
/admin/support-tickets/:id        (18, or a drawer off 17)
/admin/feature-flags              (20)
/setting-management               (21, stock ABP, already registered)
/identity/users                   (05, stock ABP, already registered)
/identity/roles                   (22/23, stock ABP, already registered)

NOT ROUTED — descoped or blocked:
/admin/campaigns, /admin/campaigns/:id     (14/15 — wrong realm)
/admin/reports                             (16 — wrong realm)
/admin/audit-logs                          (19 — no backend)
/admin/notifications                       (25 — no concept)
/admin/payments/:id                        (12 — no endpoint)
```

`route.provider.ts` currently only has a menu entry for `/home` and `/admin/businesses`. Every other real
page above needs its own `routes.add([...])` entry (with `requiredPolicy` where applicable) before it's
reachable from the sidebar — this is a small but easy-to-forget step for each new page (learned from
building §03: the component and route both existed and compiled, but no menu link was added until this
document's Route Map called it out).

## 7. Implementation Order

Ordered by (a) real backend availability first, (b) shared-component payoff, (c) dependency chains:

1. **Extract shared components** (`DataTable`, `Pagination`, `StatusBadge`, `SearchInput`,
   `EmptyState`, `ConfirmDialog` wrapper) from the existing Businesses page — pure refactor, no new API
   surface, pays for itself immediately.
2. **Categories** (§13) — simplest fully-real CRUD page, good second example of the pattern with a create/
   edit modal (Businesses has none).
3. **Support Tickets + Support Ticket Details** (§17/18) — real, moderately complex (thread/reply UX),
   high product value (Support Agent workflow).
4. **Business Details — Overview + Tickets tabs only** (§04) — depends on #3 for the Tickets tab
   component; Billing tab deferred.
5. **Plans** (§09) — real CRUD, introduces the card-grid layout pattern and JSON-field form handling.
6. **Subscriptions + Payments/Invoices** (§07/§11) — real but needs the tenant-name-resolution helper
   (§5 Shared Services) built first; ship these two together since they share that dependency.
7. **Dashboard** (§02) — deliberately late: it's a composition of widgets from pages #2–6, so building it
   last means every widget it links to already exists and is real.
8. **Feature Flags** (§20) — stock ABP wrapper, low effort, can be slotted in anytime after #1.
9. **Route/menu wiring for Users, Roles, Settings, Admin Profile** (§05, §22/23, §21, §26) — these are
   stock ABP modules already installed and already have working routes for `/setting-management`,
   `/identity/*`, `/account/*`; the work here is menu entries + confirming they render inside the same
   layout shell as the custom pages, not new development.
10. **Everything backend-blocked** (Subscription/Payment Details, Audit Logs, Campaigns, Reports,
    Notifications, standalone Permissions) — do not schedule until the corresponding `[MISSING
    REQUIREMENT]` is resolved by a backend change or a product decision to descope.

## 8. MVP vs. Future Classification

**MVP (real, buildable now):** Login, Dashboard (reduced), Businesses ✅, Business Details (partial),
Users (as Host-realm-only "Customers"), Subscriptions (no search), Plans, Payments (as Invoices),
Categories, Support Tickets + Details, Feature Flags, Settings (generic only), Roles/Role Details, Admin
Profile.

**Future — blocked on backend work:** Subscription Details, Payment Details, Audit Logs, true cross-tenant
User Details, standalone Permissions catalog.

**Future — blocked on product decision:** Campaigns/Campaign Details (rescope as Business Portal, or
define a real platform-announcement concept), Reports (define real platform-wide metrics), Notifications
(define what it actually means for a Host-realm user).

## 9. Risks and Missing Requirements (consolidated)

1. 🔴 **No platform-wide Reports/Analytics backend.** `ReportsController` is tenant-scoped. Biggest gap
   relative to the original design docs' ambition for this portal.
2. 🔴 **No platform-wide Campaigns backend.** Same wrong-realm issue as Reports.
3. 🔴 **Audit Logs have no REST API exposed**, despite data being recorded. One-line-ish backend fix
   (`[DependsOn(typeof(AbpAuditLoggingHttpApiModule))]`), but it IS a backend change, not Angular work.
4. 🔴 **No `Payment` entity endpoint at all** — only `Invoice`. No refund capability anywhere.
5. 🔴 **`Subscription Details` and `Payment/Invoice Details` have no `GetAsync(id)`** — list-only
   endpoints. Do not fake single-record pages via router-passed state.
6. 🔴 **Cross-tenant "Users" search** (the docs' actual vision) doesn't exist — stock ABP Identity is
   Host-realm-scoped only, which for this app's data model means "customers," not "all staff everywhere."
7. 🔴 **No aggregate/count fields** on several list DTOs the prototype's UI implies exist: tenant count per
   plan, business count per category, member/branch count per business. Each would need either a new
   endpoint or an expensive N+1 client pattern — recommend the former if these are genuinely wanted.
8. 🔴 **"Admin Notifications" has no defined product meaning** — needs a decision before it can be
   specced, let alone built.
9. ⚠️ **Routing gap**: `redirectAuthenticatedToHomeGuard` sends every authenticated login to `/home`
   (the customer/tenant placeholder), never to `/admin`. A Host-realm admin logging in today lands on the
   wrong page. Needs a realm-aware redirect (e.g. check `currentUser.tenantId === null` **and** presence
   of a Host-realm permission like `Tenants.View` before deciding `/home` vs. `/admin`) before this portal
   is usable end-to-end, independent of any individual page's own readiness.
10. ⚠️ **No shared layout/shell component built yet** beyond ABP's own `DynamicLayoutComponent` — no
    custom topbar, no user-menu, no sidebar styling pass matching `prototype/admin/*.html`'s visual design.
    Every page in this plan assumes that shell exists; it's cross-cutting infrastructure work not owned by
    any single page section above.
11. ⚠️ Several `[Consts]`-driven validation limits (max name lengths, JSON blob size limits) were
    referenced by name (`CategoryConsts.MaxNameLength`, `SubscriptionPlanConsts.MaxNameLength`, etc.) but
    their actual numeric values weren't read in this research pass — trivial to confirm, just not done
    here, so form `[StringLength]` maxes in this doc are structurally correct but numerically unconfirmed.
