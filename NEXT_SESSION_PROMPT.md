# Paste this to start the next Claude Code session

I'm continuing work on **Eksabli**, a bilingual (Arabic + English, RTL) loyalty platform built on
ABP Framework 10.5 (.NET 10) + Angular 21, PostgreSQL. Read [`CLAUDE.md`](CLAUDE.md) at the repo
root first (real dev commands, architecture, Mapperly mapping convention, Excel-export pattern).
Then read this whole file before doing anything — it exists so you don't repeat mistakes/rediscover
things already settled.

**Backend status: essentially done, but not immutable.** Features 01-08 (identity/multi-tenancy,
businesses, loyalty engine, billing/subscriptions, categories, support tickets, etc.) were fully
implemented before Angular work started — don't assume the backend needs building from scratch; verify
capabilities by reading the actual code, not the docs (the docs under `docs/eksabli-loyalty-platform/`
describe the target design and can be stale vs. what's actually implemented). That said, several small,
justified backend additions (new DTO fields, new aggregate/stats endpoints, and — this session — a
genuinely new business-facing app service method, `MembershipAppService.GetMembersAsync`) have already
landed *during* frontend work when the UI needed real data with no existing endpoint — see gotcha #6
and the Business Portal section below. Keep verifying against real code, not assuming either "the
backend never changes" or "the backend has everything."

**Current work: building the Angular UI for ONE portal shell (`/admin/*`) serving TWO realms**, on
branch `MD/admin-portal-frontend`, feature by feature, backend-verified, build-verified at every step.
`AdminLayoutComponent`/`/admin` is used by both Host-realm platform staff (Businesses, Users,
Categories, Plans, Subscriptions, Support Tickets, System group — gated on Host-only permissions like
`Eksabli.Tenants.View`/`Eksabli.Users.View`) AND tenant-realm business staff (Customers — gated on
`Eksabli.Memberships.View`, a real per-tenant permission, NOT `Eksabli.Tenants.View`). This was a **separate `/business/*`
route tree with its own `BusinessLayoutComponent` shell earlier this session** — merged back into
`/admin` after the user pointed out the two-portal split was unnecessary; see the "Customers" section
below for the full reasoning and what changed. Don't recreate a separate `/business` shell — add any
future tenant-realm-staff page as another `/admin/*` child instead, gated on its own real permission
(never `Eksabli.Tenants.View`, which tenant-realm staff don't hold).
Admin Portal MVP scope is now fully built (Dashboard shipped this session, see "What's NOT done"
below for the few remaining backend-blocked items) plus Users, an explicit beyond-MVP addition.

## Source-of-truth priority (in order) — never violate this

1. The actual backend codebase (controllers, app services, DTOs, permissions — read the real
   files, don't guess).
2. [`docs/eksabli-loyalty-platform/admin-portal-backend-readiness.md`](docs/eksabli-loyalty-platform/admin-portal-backend-readiness.md)
   — gap analysis of what the backend actually supports per Admin Portal feature.
3. [`docs/eksabli-loyalty-platform/admin-portal-implementation-plan.md`](docs/eksabli-loyalty-platform/admin-portal-implementation-plan.md)
   — the approved Angular implementation plan.
4. `prototype/admin/*.html` — a static Tailwind mockup with **demo data**. It is a visual/UX
   reference only, lowest priority. It contains fields/stats that don't exist in the real backend
   (see "Prototype vs. real backend" gotcha below) — never copy it uncritically.

## Hard rules — do not violate

- **Never invent APIs or fake data.** If a capability the UI wants doesn't exist on the backend,
  either (a) add the real backend capability (domain → contracts → app service → mapper, see
  gotcha #6 below) if it's small and clearly justified, and say so explicitly, or (b) mark it
  `[MISSING BACKEND CAPABILITY]` in a code comment and skip it. Never mock/hardcode data to make a
  screen look complete, and never silently drop a stat that can't be computed (e.g. "Churn" — no
  time-series endpoint exists for it — just don't show it, don't fabricate a number).
- **Never disable tenant filters from Angular; multi-tenancy is backend-authoritative.** When a
  platform-wide admin view legitimately needs cross-tenant data (e.g. Categories' business count,
  Subscriptions, Businesses list), that's a *backend* change using
  `IDataFilter.Disable<IMultiTenant>()` inside the app service — see `AdminTenantAppService.cs` and
  the now-similarly-patterned `CategoryAppService.cs` for the reference shape. Angular just calls
  the resulting endpoint.
- **Classify every API before wiring it**: Host-only / Tenant-scoped / Cross-tenant admin /
  platform-level — and route-guard + permission-check accordingly (see `app.routes.ts` — note
  Subscriptions is deliberately gated on `Eksabli.Billing.ManagePlatform`, not the `Tenants.View`
  every other admin route uses, because that controller has no anonymous/lesser read).
- **Reuse, never duplicate**: `AdminLayoutComponent` (`angular/src/app/admin/layout/`), and the
  shared component library in `angular/src/app/shared/components/` — `PageHeaderComponent`,
  `SearchInputComponent`, `StatusBadgeComponent`, `LoadingSpinnerComponent`, `ErrorStateComponent`,
  `EmptyStateComponent`, `PaginationComponent`, `ModalComponent`. Also reuse the established action-
  button convention (`btn btn-sm btn-outline-secondary`/`btn-outline-danger` icon buttons) across
  every table — don't switch one page to the prototype's borderless icon style while others keep
  the bordered one; that was a deliberate consistency call, not an oversight.
- **Work incrementally, one feature at a time, build-verified before moving on** — this is the
  explicit standing instruction. Never implement multiple features in one uninterrupted pass.

## Verification checklist — run this for every feature/change, every time

1. Inspect the real backend (controller, app service, DTOs, permission names) before writing any
   Angular code.
2. Implement (standalone component, signals, `OnPush`, Reactive Forms, `input()`/`output()`,
   `inject()`, native control flow — full list in `.claude/CLAUDE.md`'s Angular section).
3. If you touched `en.json`/`ar.json`
   (`src/Eksabli.Domain.Shared/Localization/Eksabli/`), validate both parse:
   `node -e "JSON.parse(require('fs').readFileSync('src/Eksabli.Domain.Shared/Localization/Eksabli/en.json','utf8'))"`
   (and same for `ar.json`) — an empty-string value for a key makes ABP's pipe render the raw key
   instead of falling back sensibly; don't add empty-string localization values, ever.
4. `cd angular && npx ng build --configuration development` — must be clean, zero errors.
5. `npm run lint` — must show only the one pre-existing unrelated `footer.component.ts`
   selector-prefix error, nothing new.
6. If you touched backend C#: `dotnet build src/Eksabli.Application/Eksabli.Application.csproj`
   (or the specific project you touched) — **do not run a full solution build if
   `Eksabli.HttpApi.Host` might be running locally** (IIS Express/Kestrel dev server) — its DLL
   will be locked and the copy step fails with `MSB3027`/`MSB3021`, which is a false-alarm lock
   error, not a compile error. Build the specific library project instead to get a clean signal.
7. Verify: routes wired in both `app.routes.ts` (lazy load + `permissionGuard` +
   `data.requiredPolicy`) and `route.provider.ts` (menu entry, `layout: eLayoutType.empty` for
   admin pages, correct `requiredPolicy`); permissions match the real backend attribute, not a
   guess; localization keys all exist in both `en.json` and `ar.json`; responsive at narrow
   viewports (no inline `style="min-width/max-width"`, no fixed-width grid columns without a
   `col-12 col-sm-*` mobile fallback); every prior admin page still builds/works.
8. Summarize what changed and what's still `[MISSING BACKEND CAPABILITY]`, if anything.

## What already exists (Angular Admin Portal, uncommitted on `MD/admin-portal-frontend`)

All of the below is implemented and build-verified, but **not yet committed** — `git status` shows
everything as modified/untracked working-tree changes. Review before committing.

- **Phase 1 foundation**: `angular/src/app/core/guards/admin.guard.ts` (`adminGuard`,
  `isPlatformAdmin()`, `ADMIN_REALM_PERMISSION = 'Eksabli.Tenants.View'`);
  `redirectAuthenticatedToHomeGuard` in `app.routes.ts` routes platform admins to `/admin`;
  `AdminLayoutComponent` (custom shell — sidebar, topbar, language switcher, dark mode, logout —
  registered as `eLayoutType.empty` so ABP's own Lepton-X layout doesn't also wrap it); shared
  component library (8 components, listed above).
- **Businesses** (`admin/businesses/`) — tenant list, approve/suspend actions. Extended this session
  to match `prototype/admin/businesses.html`'s table shape where the data is genuinely real: added
  **Category** (resolved from `CategoryId` via bulk `CategoriesService.getList`, `[AllowAnonymous]`),
  **Members** (new real backend field — `AdminTenantDto.MemberCount`, active-`Membership` count per
  tenant computed in `AdminTenantAppService` via the same `IDataFilter.Disable<IMultiTenant>()`
  pattern as the rest of that DTO — see gotcha #6-style addition, `GetMemberCountLookupAsync` does a
  DB-level `GroupBy`/`Count`, not a client-side load), and **Plan** (best-effort bulk
  `AdminSubscriptionsService.getList` mapped by `tenantId`, column hidden entirely for a viewer
  without `Eksabli.Billing.ManagePlatform` rather than erroring — that controller has no lesser read).
  Avatar-initials treatment added to match the prototype visually. `[MISSING BACKEND CAPABILITY]`,
  intentionally not built: a Plan **filter** dropdown (`AdminTenantFilterDto` has no `planId` field —
  client-side filtering of server-paginated results would silently misreport counts) and the
  prototype's "Trialing" status option (that's really `TenantSubscriptionStatus`, not a real
  `BusinessProfile.ApprovalStatus` value — only Pending/Approved/Suspended exist).
  **Naming bug found and fixed**: the page title/breadcrumb/sidebar entry all reused the
  `Menu:AdminTenants` localization key, whose **English** value was literally "Tenants" — while the
  **Arabic** value already correctly said "Businesses" (`الأنشطة التجارية`), an inconsistency nobody
  had caught. Fixed by correcting the English value to "Businesses" (kept the key name as-is,
  `Menu:AdminTenants`, to avoid touching every reference site for a text-only fix) — this
  automatically fixed the page title, breadcrumb, and `AdminLayoutComponent` sidebar entry all at
  once, since they all resolve the same key.
  **"New Business" create action added** — wired to a REAL, already-implemented endpoint,
  `BusinessController.RegisterAsync` (`[AllowAnonymous]`, the same one the public self-service signup
  flow uses — confirmed via `BusinessAppService.RegisterAsync`, not a new API). One call provisions
  the Tenant + owner IdentityUser + BusinessProfile + first Branch + a trial TenantSubscription
  together. Modal form matches `RegisterBusinessDto`'s real fields/validators exactly (checked actual
  `MaxLength` consts server-side — `BusinessProfileConsts`/`BranchConsts` — rather than guessing).
  Gated client-side on `Eksabli.Tenants.Approve` (closest real "trusted with business lifecycle"
  permission — there's no dedicated `Tenants.Create`, and the endpoint itself has no server-side
  permission check at all, by design, for public signup).
  **Extended further same session** — user pointed out `Branch.Latitude`/`.Longitude` and
  `BusinessProfile.SocialLinksJson` already exist on the backend entities (confirmed) but weren't
  reachable from `RegisterBusinessDto`/the create-business form at all. Added `BranchLatitude`/
  `BranchLongitude` (double?, wired to the existing `Branch.SetLocation`) and `InstagramUrl`/
  `FacebookUrl` (string?, serialized into the same freeform `SocialLinksJson` blob via a small
  `BuildSocialLinksJson` helper in `BusinessAppService` — "instagram"/"facebook" are just the two keys
  this endpoint happens to populate, not a schema the column itself enforces; the tenant's own
  `UpdateBusinessProfileDto` still exposes the raw JSON directly, untouched). Latitude/longitude are
  captured for a **future** "nearest branch" distance feature — confirmed nothing in the backend
  computes or exposes distance today, so this session only added the data-capture half, not a
  distance calculation; don't assume that exists. Reused the real, already-shipped `Latitude`/
  `Longitude` localization keys (originally from the tenant-side Branch CRUD UI) rather than
  inventing new ones.
- **Categories** (`admin/categories/`) — full CRUD. Just extended with a real **`businessCount`**
  column (backend change: `CategoryDto.BusinessCount`, computed in `CategoryAppService` via
  `IDataFilter.Disable<IMultiTenant>()` over `BusinessProfile.CategoryId`, Mapperly
  `[MapperIgnoreTarget]` for the manually-set field — see gotcha #6). Counts every `BusinessProfile`
  regardless of `ApprovalStatus` (pending/approved/suspended all count) — flagged to the user as a
  possible follow-up if they want approved-only.
- **Plans** (`admin/plans/`) — card-grid CRUD, feature-limits JSON editor.
- **Subscriptions** (`admin/subscriptions/`) — table with expandable-row invoice sub-list (no
  routed details page — backend has no `GetAsync(id)`), Record Payment modal, and a real-data stat
  row (Active count / Trialing count / approx. MRR — all from real endpoints, see gotcha #7).
  **Fixed this session**: the stat row originally cost 2 extra concurrent `GetListAsync` calls against
  `/api/app/admin-subscriptions` (status=Active fetching up to 500 rows just to sum MRR client-side,
  plus a separate status=Trialing count-only call) on top of the paginated table's own call to the
  *same* endpoint — 3 simultaneous requests to one endpoint on every page load. User caught this via a
  server log `OperationCanceledException` (harmless — client navigated away before all 3 finished, but
  the concurrency itself was real waste). Added a real, dedicated `AdminSubscriptionAppService
  .GetStatsAsync()` (`GET /api/app/admin-subscriptions/stats`) — DB-level `GroupBy` over active
  subscriptions joined against plan prices, one round trip, and now a TRUE total MRR (not a
  first-500-rows approximation). `admin-subscriptions.component.ts`'s `loadStats()` calls it directly;
  `SubscriptionPlansService` is no longer injected there (no longer needed client-side for this).
- **Support Tickets** (`admin/support-tickets/`) — queue table (status + priority filters, no
  search — `SupportTicketFilterDto` has no `filterText`) with a modal-based thread/detail view
  (reply form + Mark Resolved), matching the implementation plan's own recommendation that ticket
  detail be a drawer/panel, not a routed page. `[MISSING BACKEND CAPABILITY]`: no reopen/explicit
  in-progress transition — only `ResolveAsync` is exposed (`SupportTicket.Reopen()`/`.Close()` exist
  on the domain entity but aren't wired through `ISupportTicketAppService`), so "Mark Resolved" is
  the only status-changing action. "From" column resolves `tenantId` to a business name via the same
  bulk `AdminTenantsService` lookup Subscriptions uses; a customer ticket's `customerId` is
  deliberately NOT resolved to a user name (would need one `IdentityUserService.get()` call per row —
  N+1 — against a permission a Support Agent may not hold) — shown as a generic "Customer" label
  instead. No backend changes needed for this feature — it was already fully READY per the gap
  analysis.
- **Users** (`admin/users/`) — cross-tenant/cross-realm user directory, matching
  `prototype/admin/users.html`. Backed by a **brand-new endpoint**,
  `AdminUserAppService.GetListAsync` (`GET /api/app/admin-users`, new permission
  `Eksabli.Users.View`, its own dedicated permission — not reused as `Tenants.View`, since it exposes
  contact info across every tenant, a more sensitive scope worth auditing independently) — no existing
  endpoint combined Host-realm customers with cross-tenant business staff. Combines two genuinely
  different sources in-memory (same "acceptable at this scale" approach every other cross-cutting
  admin list here uses):
  - **Customers**: Host-realm `CustomerProfile` (name) + `IdentityUser.PhoneNumber` (contact). Every
    real registered customer — a customer isn't owned by one business, so there's no tenant filter.
  - **Staff**: cross-tenant `EmployeeAssignment` (`IDataFilter.Disable<IMultiTenant>()`) + each
    assignment's own tenant-scoped `IdentityUser.Email` (contact) + that assignment's real
    `Tenant.Name` (business) — a genuine per-row lookup, unlike the prototype's own demo data, which
    hardcodes "Cedar & Bean Coffee" for every employee.
  - `Realm` (Host/Tenant) is derived client-side from `Type`, not a stored field — architecturally
    fixed by construction (a customer is always Host-realm, staff always tenant-realm).
  - **Real gap found and NOT reproduced**: the prototype shows an "Invited" status for some employees
    — searched every place an employee `IdentityUser` is created
    (`EmployeeAssignmentAppService.InviteAsync`, the owner-creation path in `BusinessAppService
    .RegisterAsync`) and confirmed accounts are created **active immediately**, no pending-invite
    state exists in the domain. Status is real `IdentityUser.IsActive` only — Active/Inactive.
  - **Real gap found and NOT reproduced**: `IdentityUser.Name`/`.Surname` are never populated
    anywhere in this codebase for either realm (confirmed by searching every `new IdentityUser(...)`
    call) — a Staff row's name is always null; the template falls back to showing their email instead
    of a placeholder name (misleading) or the customer-style "Unnamed customer" text (also wrong —
    staff genuinely have no name field, customers just might not have filled theirs in yet; these are
    different situations, not the same fallback).
  - Platform-staff accounts (seeded Host admin, Support Agent, etc.) are deliberately excluded, same
    scope as the prototype — they belong on the stock Identity > Users page, not this directory.
- **Sidebar nav is grouped** (`AdminLayoutComponent`'s `ADMIN_NAV: AdminNavGroup[]`) — "Business"
  (Customers — tenant-realm, see its own section below), "Platform" (Businesses, Users, Categories),
  "Billing" (Plans, Subscriptions), "Operations" (Support Tickets). Only groups with ≥1 real, built
  page exist — no placeholder/disabled nav entries for unbuilt features.
- **Customers** (`admin/customers/`, tenant-realm data — Members + Following tabs) — see the dedicated
  "Customers page (tenant-realm data, inside the Admin Portal shell)" section below for the full story
  (originally a separate Business Portal, folded back into this shell after the user questioned the
  `/business` route split).
- **Business Details** (`admin/businesses/admin-business-details.component.*`,
  `/admin/businesses/:tenantId`) — routed drill-down from the Businesses list (row's name is now a
  link). Left profile card (avatar-initial, name, category — best-effort `CategoriesService.get`,
  status badge, Signed up date, Business ID, Approve/Suspend) + right-side tabs: **Overview** (one
  real stat, "Open Support Tickets" via a `status=Open, maxResultCount:0` filtered count, hidden for
  a viewer without `SupportTickets.Manage`), **Billing** (`[MISSING BACKEND CAPABILITY]` —
  `AdminSubscriptionFilterDto` has no `tenantId` filter, so this tab shows a "not available yet" empty
  state + a link to the full Subscriptions page rather than the unscalable "fetch every subscription
  and filter client-side" workaround), **Support Tickets** (real, `SupportTicketFilterDto.TenantId`-
  filtered table, same column shape as the full queue but **read-only** here — no reply/resolve, per
  the implementation plan's own Forms/Actions sections for this page; use the full Support Tickets
  page for triage). No Activity Log tab — no audit log API exists. 404 (invalid `tenantId`) is
  handled as a distinct "not found" state, not the generic error state. Route needed **no** new
  `route.provider.ts` entry — confirmed `findRoute()` (abp-ng.core) walks a path up by segment until
  it hits an exact match, so `/admin/businesses/:tenantId` inherits `/admin/businesses`'s own
  `eLayoutType.empty` entry automatically. Worth knowing for any future detail/:id route.
- **"System" nav group** (`AdminLayoutComponent`) — Users, Roles, Settings, My Profile, and Tenant
  Management, all pointing at **stock ABP pages** (`@abp/ng.identity`, `@abp/ng.setting-management`,
  `@abp/ng.account`, `@abp/ng.tenant-management`). Two real bugs were found and fixed getting this
  right this session — both matter for any *future* stock-ABP nav addition too:
  1. **Chrome-consistency bug**: first shipped pointing at these packages' own **top-level** paths
     (`/identity/users`, `/setting-management`, `/account/manage`) — since those routes live outside
     the `/admin` route tree `AdminLayoutComponent` wraps, clicking them jumped to Lepton-X's stock
     SideMenu chrome (different sidebar/theme entirely, screenshot-confirmed by the user). Fixed by
     **nesting** each package's own `createRoutes()` under `/admin` in app.routes.ts too
     (`/admin/identity/users`, `/admin/setting-management`, `/admin/account/manage`, `/admin/tenant-
     management/tenants`) — safe because each package's `createRoutes()` already ships its own
     `authGuard`/`permissionGuard` + per-leaf `data.requiredPolicy` (`AbpIdentity.Users`/`.Roles`,
     `AbpTenantManagement.Tenants`, `authGuard` for Account), so nesting only changes which chrome
     renders, not what's guarded. Added matching `invisible: true` layout-anchor entries in
     route.provider.ts (`/admin/identity`, `/admin/setting-management`, `/admin/account`, `/admin/
     tenant-management`) so `findRoute()`'s path-walk-up resolves `eLayoutType.empty` for everything
     nested under them, without becoming their own (unwanted) menu items. The packages' original
     top-level mounts were deliberately left in place, not removed — `/account` must stay reachable
     pre-auth for the actual login flow, and leaving `/identity`/`/setting-management`/`/tenant-
     management` at their old paths too is a harmless fallback now that nothing in our own nav points
     there anymore. **Always nest under `/admin`, never link to a package's own top-level mount.**
  2. **Stale-UI bug**: after fix #1, stock pages loaded data correctly (network call succeeded) but
     stayed visually empty until an unrelated second click. Root cause: `AdminLayoutComponent` was
     `ChangeDetectionStrategy.OnPush`; stock ABP components (`UsersComponent` etc.) update state via
     plain property assignment (`this.data = res`), not signals. A signal write auto-propagates a
     dirty-mark up through OnPush ancestors (Angular's reactivity integration does this specially); a
     plain assignment does not — so Angular's CD tree walk hit the OnPush shell, found it not dirty,
     and pruned its entire subtree (the `<router-outlet>` content) — until some unrelated click on the
     shell's own template (e.g. a nav link's `(click)` handler) forced it to be checked. **Fixed by
     removing OnPush from `AdminLayoutComponent` specifically** (see its code comment) — a deliberate,
     justified exception to `.claude/CLAUDE.md`'s default, since this component's job is to host
     arbitrary routed content it doesn't control the reactivity model of. Our own signal-based admin
     pages were unaffected either way. **Any future shell/wrapper hosting non-Eksabli routed content
     must not default to OnPush.**

  Tenant Management (`/admin/tenant-management/tenants`) exposes raw `Volo.Abp.TenantManagement.Tenant`
  records (connection strings, deactivate/delete) — a genuinely different concept from our own
  "Businesses"/`BusinessProfile` approval-workflow page; both legitimately coexist in nav. Gated on the
  real stock `AbpTenantManagement.Tenants` permission (SuperAdmin-only by default), not reused as
  `Eksabli.Tenants.View`. **Still deliberately skipped**: a standalone "Feature Flags" nav entry —
  `FeatureManagementComponent` is a **modal** requiring a `providerKey`/`providerName` (a specific
  tenant), normally opened via a "Manage host features" button that lives *inside* Tenant Management's
  own stock list component — not something a separate Eksabli nav entry can target on its own, so a
  bare link to it would still be a dead end.

## Conventions and gotchas learned the hard way — don't rediscover these

1. **Angular i18n must go through ABP's actual localization pipeline.** Keys live in
   `en.json`/`ar.json` under `AdminPanel:<Feature>:*` / `Menu:*` namespaces, referenced as
   `{{ '::Namespace:Key' | abpLocalization }}` (`LocalizationPipe`). Reuse real framework keys
   where they exist instead of inventing duplicates (e.g. `'AbpUi::Logout'`, found in Lepton-X's
   own source, not guessed). Language switching:
   `RouteBasedCultureUrlService.applyLanguageSelection(cultureName)`. Current language as a signal:
   `toSignal(sessionState.getLanguage$(), { initialValue: sessionState.getLanguage() })`; direction
   via `getLocaleDirection()`, bound to `[attr.dir]` on the shell root.
2. **Layout/chrome is resolved via `route.provider.ts`'s menu tree, not just a route's own
   `data.layout`.** `DynamicLayoutComponent` calls `findRoute()` against the `RoutesService` tree;
   an ancestor menu entry's `.layout` can override the route's own. Every admin leaf route needs an
   exact-path entry in `route.provider.ts` with `layout: eLayoutType.empty`, even though
   `AdminLayoutComponent`'s own sidebar is built from a separate plain array
   (`ADMIN_NAV`/`AdminNavGroup[]`), not from this menu tree — the two lists currently have to be
   kept in sync by hand.
3. **RTL**: use CSS logical properties (`inset-inline-start`, `border-inline-end`) not physical
   ones. `translateX()` has **no logical equivalent** — flip its sign explicitly under `:dir(rtl)`
   (a real CSS pseudo-class driven by the `dir` attribute, not an Angular binding) — see
   `admin-layout.component.scss`'s sidebar drawer transform for the pattern.
4. **Verify with an actual build, not just reading the diff** — trust the compiler/real package
   source over recollection of framework internals.
5. **I (the user) strongly prefer direct execution over being asked to confirm process/workflow
   questions.** Proceed with the obviously-intended work; reserve actual questions
   (`AskUserQuestion`) for genuine product/design forks with no clearly-correct default — e.g.
   "restyle the sidebar into grouped sections now or later" was a legitimate ask; "should I run
   `ng build`" is not.
6. **Adding a small, justified backend capability the Angular layer needs is fine** — it doesn't
   violate "don't invent APIs" as long as it's a real, server-computed field wired through the real
   layers (Domain-Shared consts → Contracts DTO → Application service → HttpApi controller if
   needed → Mapperly mapper), not a shortcut. Reference shape: adding `CategoryDto.BusinessCount`
   this session — cross-aggregate fields not present on the source entity need
   `[MapperIgnoreTarget(nameof(Dto.Field))]` on **both** mapper overloads (see
   `EksabliTenantSubscriptionToTenantSubscriptionDtoMapper`'s `PlanName` for the existing pattern),
   then the app service sets the field manually after `ObjectMapper.Map(...)`. After any such
   change, the Angular proxy model (`angular/src/app/proxy/**/models.ts`) needs the matching field
   added by hand if you can't run `abp generate-proxy -t ng` against a live, freshly-restarted Host
   — tell the user to run it themselves once they restart the Host, to confirm the shape matches.
7. **Prototype vs. real backend**: the static prototype (`prototype/admin/*.html`) contains
   aspirational/demo-only stats that don't correspond to any real endpoint — e.g. Subscriptions'
   "Churn (30d)" (no time-series data exists to compute it; don't fabricate it) and status values
   like "Pending Approval"/"Suspended" shown in a *subscription* status column (that's actually
   `BusinessProfile.ApprovalStatus`, a different concept, already shown correctly on the
   Businesses page — don't conflate the two). Where a prototype stat genuinely IS computable from
   real data (e.g. Subscriptions' Active/Trialing counts via a `maxResultCount: 0` filtered
   `totalCount`, or an *approximate* MRR by summing real `SubscriptionPlanDto.monthlyPrice` across
   active subscriptions), build it for real and label estimates as estimates (tooltip/hint text)
   rather than presenting them as precise. "Payments" and "Reports" as separate nav items are out
   of MVP scope per an earlier explicit descoping decision — don't reintroduce them because the
   prototype has them.
8. **MVP scope exclusions** (explicit, don't reintroduce): no Campaigns / Campaign Details /
   Reports / Payment Refunds / standalone Notifications pages. "Payments" is rescoped to
   "Invoices" terminology throughout.
9. **`maxResultCount: 0` is NOT a valid way to get "just the totalCount, no items" — it 400s.**
   Every ABP list endpoint's filter DTO inherits `LimitedResultRequestDto.MaxResultCount`, which
   carries `[Range(1, int.MaxValue)]` (confirmed by reflecting the actual compiled
   `Volo.Abp.Ddd.Application.Contracts.dll`, not guessed) — combined with `[ApiController]`'s
   automatic model validation, `maxResultCount: 0` fails with an HTTP 400 before the app service
   method even runs. This shipped broken in two places before the user caught it live by hitting
   `/api/app/support-ticket?...&maxResultCount=0` directly: `admin-business-details.component.ts`'s
   "Open Support Tickets" stat and `admin-dashboard.component.ts`'s Open Tickets tile — both fixed to
   `maxResultCount: 1` (the lowest accepted value; still reads `totalCount` correctly, the one
   returned item is simply ignored). **Always use `1`, never `0`, for a filtered-count-only call** —
   grep `maxResultCount:\s*0` before shipping any new stat tile that reuses this "count via a filtered
   list call" pattern.

## Customers page (tenant-realm data, inside the Admin Portal shell)

This section covers `admin/customers/` — the one page in the Admin Portal that serves tenant-realm
business staff (Owner/BranchManager/Cashier) rather than Host-realm platform staff. Content/data is
unchanged from when it was first built; only the **routing/shell** changed, twice, this session:

- **First cut**: built as a genuinely separate **Business Portal** — its own `/business/*` route tree,
  its own `BusinessLayoutComponent` shell (near-duplicate of `AdminLayoutComponent`, ~150 lines of
  copy-pasted SCSS), mounted at `/business/customers`.
- **User then asked "the url /business/customers not correct why add under business"** — questioning
  the separate route namespace. Investigation confirmed: ABP tenant resolution has **nothing to do
  with the Angular route path**. This app uses ABP's default resolver chain (`app.UseMultiTenancy()`
  in `EksabliHttpApiHostModule.cs`, no custom subdomain/path resolver configured) — for an
  authenticated user, `CurrentUserTenantResolveContributor` resolves `CurrentTenant.Id` straight from
  the logged-in user's own account (business staff `IdentityUser`s are created *inside* their tenant's
  identity space at registration, per `BusinessAppService.RegisterAsync`). So `/business` vs `/admin`
  vs anything else was **purely an Angular route-namespace choice**, never a backend requirement.
- **User then said "must be under admin"** — an explicit instruction to fold Customers into the one
  Admin Portal shell instead of maintaining a second, nearly-identical one. Done:
  - `admin/customers/admin-customers.component.*` (renamed from `business/customers/business-
    customers.component.*`, class `AdminCustomersComponent`) — same real data/logic, unchanged.
  - `business/layout/business-layout.component.*` **deleted** — the entire `business/` directory is
    gone; don't recreate it.
  - `app.routes.ts`: `customers` is now a child of `/admin` (`Eksabli.Memberships.View` via
    `permissionGuard`, same as before). The **parent `/admin` route's `adminGuard` was relaxed to
    `authGuard` only** — `adminGuard` requires the Host-only `Eksabli.Tenants.View` signal permission
    (see `admin.guard.ts`), which tenant-realm business staff never hold by construction, so leaving it
    in place would have 403'd every business-staff visit to `/admin` before even reaching the child's
    own guard. This is safe: every other `/admin/*` child already enforces its own
    `permissionGuard` + specific real policy (or, for nested stock-ABP children, that package's own
    self-contained guards) regardless of the parent gate — nothing is newly reachable that wasn't
    already permission-checked at the leaf.
  - `route.provider.ts`: entry moved to `/admin/customers` (still `Eksabli.Memberships.View`).
  - `AdminLayoutComponent`'s `ADMIN_NAV`: added a new **"Business" group** (`::AdminPanel:Layout
    :GroupBusiness`), listed *first*, containing only Customers — deliberately first because it's
    likely the *only* visible group for a business-staff account (every other group is gated on
    Host-only permissions they don't hold); the per-group empty-after-filter pruning already in
    `navGroups` handles hiding groups a given user can't see, same as before.
  - `.btn-icon` global utility (promoted to `styles.scss` when the two shells briefly coexisted) was
    **left as a global utility**, not moved back into `admin-layout.component.scss` — broadly reusable
    regardless of the second shell's removal, no reason to revert it.
  - Dropped now-unused localization keys `BusinessPanel:Layout:BusinessBadge/ToggleMenu/
    ToggleLanguage/ToggleDarkMode` (belonged to the deleted shell); kept `BusinessPanel:Layout
    :NavCustomers` and all `BusinessPanel:Customers:*` keys (still used by the page itself — the
    `BusinessPanel` localization *namespace* name was left as-is, only the routing changed).
- **Then: "this page must appear to business not for admin"** — the user's next, separate request.
  Root issue: `Eksabli.Memberships.View` alone doesn't exclude a platform admin, because the seeded
  Host "admin" role is granted **every** permission that exists at `DbMigrator` seed time (see
  `IdentityDataSeeder`), including business-scoped ones — so a platform admin was passing the
  permission check and could both see the "Business" nav group and open `/admin/customers`, even
  though it isn't their data (and would just render empty for them — `Membership` etc. are
  tenant-scoped, `CurrentTenant.Id` is null for a Host admin). Fixed with an explicit realm exclusion,
  layered on top of (not instead of) the real permission check:
  - `core/guards/admin.guard.ts`: new `businessStaffOnlyGuard` — the inverse of `adminGuard`; redirects
    a platform admin (`isPlatformAdmin(permissionService)` true) to `/admin/businesses` instead of
    letting them through. Wired as a second `canActivate` on the `customers` child route in
    app.routes.ts (`[permissionGuard, businessStaffOnlyGuard]`), alongside the existing
    `data.requiredPolicy: 'Eksabli.Memberships.View'`.
  - `AdminLayoutComponent`: `AdminNavItem` gained an optional `businessOnly?: boolean` flag (set on the
    Customers entry); `navGroups`'s computed filter now also drops any `businessOnly` item when
    `isPlatformAdmin()` is true, so the "Business" nav group itself disappears for a platform admin
    (and the group-pruning logic already in place removes the now-empty group).
  - Net effect: Customers is reachable (nav + direct URL) only for an authenticated user who holds
    `Eksabli.Memberships.View` AND is **not** a platform admin — i.e., real tenant-realm business
    staff. A platform admin sees neither the nav entry nor the page.
- **Still true, unchanged from the original build**: no coarse "is business staff" *positive* realm
  guard exists (there's no single permission every business-staff role is guaranteed to hold — Owner/
  BranchManager/Cashier get different permission sets) — `Eksabli.Memberships.View` +
  `businessStaffOnlyGuard`'s negative platform-admin exclusion is what actually gates it now.
  `redirectAuthenticatedToHomeGuard` (app.routes.ts) still does NOT auto-route business staff anywhere
  special post-login (falls through to `/home` like any other non-platform-admin authenticated user) —
  solving real tenant-realm detection is a separate, larger piece of work than one page; don't guess
  at it.
- **Data/build shape** — mirrors `prototype/business/customers.html`'s two-tab layout (Members /
  Following), built against REAL data only:
  - **Members tab**: a **brand-new backend endpoint**, `MembershipAppService.GetMembersAsync`
    (`GET /api/app/memberships`, new permission `Eksabli.Memberships.View`) — no such business-facing
    "list my tenant's members" endpoint existed before this session (`IMembershipAppService` only had
    self-service `GetMy*` methods). Joins this tenant's `Membership` (ambient-tenant-scoped, no
    `Disable<IMultiTenant>()` needed — same shape as `FollowAppService.GetFollowersAsync`) with
    Host-realm `CustomerProfile`/`IdentityUser` (name/phone — same cross-realm join
    `PosAppService.LookupCustomerByPhoneAsync` already used for a single customer, just batched via
    `IIdentityUserRepository.GetListByIdsAsync`) and this tenant's `PointsWallet`/`Tier`
    (balance/tier). Filters/sorts/paginates in C# after loading the tenant's full member set — same
    "acceptable at this scale" approach `AdminTenantAppService` already uses, at a smaller scale by
    construction (one tenant, not cross-tenant). "Last Active" is `PointsWallet.LastModificationTime`
    (bumped by ABP's own auditing whenever a real points transaction changes the wallet) — the closest
    genuine signal, since there is no login/session tracking anywhere in this codebase; not invented.
  - **Following tab**: `FollowAppService.GetFollowersAsync` — already existed, but `FollowDto` was
    bare (no name/phone). Enriched this session via a new `FollowerDto` (same join pattern as
    `MemberDto`) rather than reusing/mutating `FollowDto`, which stays bare for the customer-facing
    `GetMyFollowsAsync` (a customer doesn't need their own name echoed back).
  - `[MISSING BACKEND CAPABILITY]`, deliberately not built: **Export** button (no Excel-export
    capability exists for Memberships, unlike Books/Authors' real token-gated pattern — nothing to
    wire it to); **"Convert to Campaign Target"** per follower (`Eksabli.Followers.ConvertToCampaign`
    permission exists but is explicitly commented "defined for parity/future use" in
    `EksabliPermissions.cs` — no endpoint backs it); a **customer-details drill-down page** (not
    built — names are plain text, not a link to a page that doesn't exist).
  - Real `MembershipStatus` is only `Active`/`Frozen` (confirmed in the domain enum) — the prototype's
    "At Risk"/"Churned" statuses don't exist anywhere in the backend and are **not** reproduced.
  - Tier filter dropdown is best-effort/hidden-on-failure: `TiersController` gates its whole
    controller (including read) on `Eksabli.Tiers.Default`, a permission a `Memberships.View`-only
    viewer doesn't necessarily also hold.

## What's NOT done — pick up here, in this order

**Dashboard (`admin/dashboard/`) is now built** — was the last Admin Portal MVP page on the original
plan, mirrors `prototype/admin/dashboard.html`, mounted at `/admin/dashboard` with a bare `/admin` ->
`dashboard` redirect (so `redirectAuthenticatedToHomeGuard`'s existing `/admin` destination for
platform admins now actually lands somewhere) and a new "Overview" nav group listed first in
`ADMIN_NAV`. Gated on `Eksabli.Tenants.View`, same as most other admin routes (no dedicated `Dashboard`
permission exists). Built **entirely from existing endpoints — no backend changes needed this time**:
- **Real, shown**: Total Businesses + Pending Approvals (count and list) from one
  `AdminTenantsService.getList` call (`maxResultCount: 500`, same bounded-batch assumption as this
  page's own bulk lookups elsewhere); Platform MRR from `AdminSubscriptionsService.getStats()` (added
  earlier this session for the Subscriptions page — the old plan below predates that endpoint and was
  wrong to rule an MRR tile out); Open Support Tickets count + Recent Support Tickets list from two
  `SupportTicketsService.getList` calls (same two-calls-one-endpoint shape `admin-business-details
  .component.ts`'s Overview tab already established as fine); Category Mix (top 5 by the real
  `CategoryDto.BusinessCount`, added earlier this session for the Categories page) from
  `CategoriesService.getList`.
- **`[MISSING BACKEND CAPABILITY]`, deliberately not built**: Daily Active Users (no login/session
  tracking exists anywhere in this codebase) and the MRR trend chart / "+X% (7mo)" growth badge (no
  historical/time-series MRR snapshot exists — `GetStatsAsync()` only gives one current-point figure).
  Both omitted entirely, not faked.
- MRR tile and the whole Recent Support Tickets widget are hidden (API calls skipped) for a viewer
  without `Eksabli.Billing.ManagePlatform` / `Eksabli.SupportTickets.Manage` respectively — same
  hide-the-tile-not-the-page pattern used elsewhere (e.g. Businesses' Plan column).
- `.eks-stat-card` (value/label stat tile) was promoted from `admin-subscriptions.component.scss` to
  a global `styles.scss` utility since Dashboard needed the identical rule — same "promote on the
  second use" pattern as `.btn-icon`/`.eks-filter-search`.

Per the backend-readiness doc's Angular Implementation Order and the user's stated MVP scope, **for
the Admin Portal**, what's left:

1. **Backend-blocked, do not build yet**: Audit Logs (no `AbpAuditLogging` HttpApi module
   reference exists), full Payments (only the manual-record-payment flow Subscriptions already has
   exists), Business Details' Billing tab (needs `AdminSubscriptionFilterDto.TenantId`) — revisit
   only once/if the corresponding backend work lands.
2. Re-check the implementation plan's Angular Implementation Order table for anything else still
   marked MVP that this file hasn't tracked — with Dashboard shipped, every page in the user's
   originally stated MVP scope should now be built; Users (`admin/users/`) was also added this session
   as an explicit user request beyond the original plan, matching `prototype/admin/users.html`.

**For tenant-realm-staff pages beyond Customers** (still mounted under `/admin/*` — see the "Customers
page" section above for why there's no separate portal shell anymore) — no equivalent
implementation-plan doc exists yet (the backend-readiness doc covers Host-realm Admin Portal features
only); scope has been driven directly by `prototype/business/*.html` + backend-reality-checking this
session. Only Customers is built. Natural next pages, if asked for: check `prototype/business/*.html`
for what exists (dashboard, campaigns, rewards, wallet/points-rules, branches, employee-assignments,
offers, coupons, reports, settings all have prototype pages — verify each against the REAL backend the
same way this session did for Customers before building, and give each its own real permission +
`ADMIN_NAV` entry, never `Eksabli.Tenants.View`; several of these app services already exist per this
session's own file exploration — `TierAppService`, `PointRuleAppService`, `WalletAppService`,
`EmployeeAssignmentAppService` was referenced but not
opened — read the actual service before assuming a page's data is available).

## Useful memory

This project also has persistent cross-session memory (separate from this file) with notes like
"repo's own status docs are stale, verify backend status via git log + src/" and the user's
preference for direct execution without process check-ins — those are already reflected in the
rules above, just noting they exist in case you're wondering why certain phrasing shows up as
settled fact.
