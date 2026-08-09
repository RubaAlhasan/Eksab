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

**Current work: building the Angular UI for TWO portals, each with its own shell**, on branch
`MD/admin-portal-frontend`, feature by feature, backend-verified, build-verified at every step:
- **Admin Portal** (`AdminLayoutComponent`, `/admin/*`) — Host-realm platform staff only
  (Businesses, Users, Categories, Plans, Subscriptions, Support Tickets, Dashboard, System group).
  Gated on `adminGuard` (`Eksabli.Tenants.View` signal permission) at the route level.
- **Business Portal** (`BusinessLayoutComponent`, `/business/*`) — tenant-realm business staff only
  (Owner/BranchManager/Cashier/MarketingManager): Customers, Employees so far. Gated on
  `businessRealmGuard` (`core/guards/business.guard.ts`) — **real tenant-resolution**, not a permission
  heuristic: it reads `currentTenant.id` from ABP's own `ConfigStateService`, which for an
  authenticated request is resolved straight from that user's own account via ABP's built-in
  `CurrentUserTenantResolveContributor` (confirmed by reading `EksabliHttpApiHostModule.cs`'s
  multi-tenancy wiring — no custom subdomain/header/query resolver is configured). See that guard's own
  comment for the full reasoning, and `redirectAuthenticatedToHomeGuard` (app.routes.ts) for how it's
  used to route a business-staff account to `/business` right after login.

**This split flip-flopped twice this session before landing here — both times at explicit, direct user
instruction, not a design mistake either time:**
1. Built as two separate portals/shells from the start.
2. User: *"the url /business/customers not correct why add under business"* → investigated, confirmed
   the route namespace was never tied to tenant resolution → user: *"must be under admin"* → Customers
   (then Employees) were folded into the Admin Portal shell, `/business/*` deleted entirely, and a
   permission-heuristic exclusion (`businessOnly` nav flag + `businessStaffOnlyGuard`) was added since
   the seeded Host admin role holds every tenant-scoped permission too (grant-all-at-seed-time).
3. User: *"employees not under admin admin/employees must be under business/employees"* +
   *"must be when login business show page related to business like prototype"* → reversed back to two
   separate portals — **but this time with a real realm-detection mechanism** (`businessRealmGuard`,
   above) instead of the permission-heuristic exclusion, since the user now also wants business-staff
   accounts auto-routed to their own portal on login, which the old heuristic couldn't support well.
   `businessStaffOnlyGuard`/`businessOnly` nav flag are gone — no longer needed, the coarse route guard
   does the whole job now.

**Don't "fix" this back to one shell without being asked again** — both directions have now been
explicitly requested once; two portals with real realm detection is where it currently stands. Add any
new tenant-realm-staff page under `/business/*` (own real permission, child of `businessRealmGuard`),
any new Host-realm page under `/admin/*` (child of `adminGuard`).

Admin Portal MVP scope is fully built (Dashboard + Users, both from earlier this session, see "What's
NOT done" below for the few remaining backend-blocked items). Business Portal has Dashboard +
Analytics + Customers + Employees + Branches + Points Management + Rewards + Coupons + Campaigns so
far — see their own section below.
**Points Management (`business/points/`) is architecturally different from every other page**: its
Award Points tab has no ABP permission at all (`PosController` is `[Authorize]`-only; the real gate is
a custom staff-role check inside `PosAppService`) — read that page's own section before assuming every
route needs a `data.requiredPolicy`.

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
- **Sidebar nav is grouped** (`AdminLayoutComponent`'s `ADMIN_NAV: AdminNavGroup[]`) — "Overview"
  (Dashboard), "Platform" (Businesses, Users, Categories), "Billing" (Plans, Subscriptions),
  "Operations" (Support Tickets). Only groups with ≥1 real, built page exist — no placeholder/disabled
  nav entries for unbuilt features. Host-realm only now — no `businessOnly` items/groups exist anymore
  (see the top of this file for why; Customers/Employees moved to their own `BusinessLayoutComponent`
  nav in `business/layout/business-layout.component.ts`, which follows the exact same "only real, built
  pages, no placeholders" rule).
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

## Business Portal — Customers + Employees

Both pages live at `business/customers/business-customers.component.*` (class
`BusinessCustomersComponent`) and `business/employees/business-employees.component.*` (class
`BusinessEmployeesComponent`), mounted under `/business/*` inside `BusinessLayoutComponent`
(`business/layout/`), gated on `businessRealmGuard` (see the top of this file for the full routing
history — both pages briefly lived at `/admin/customers`/`/admin/employees` mid-session before landing
here). `BusinessLayoutComponent` is deliberately near-identical to `AdminLayoutComponent` (same shell
shape, ~150 lines of parallel SCSS under `eks-biz-*` class names instead of `eks-admin-*`) and
deliberately NOT `OnPush`, same reasoning as `AdminLayoutComponent` (a shell wrapping arbitrary routed
content shouldn't assume every descendant uses signals) — `[MISSING REFACTOR]`, flagged not fixed:
this duplication could be extracted into a shared portal-shell component; not attempted, to avoid
risking a regression in either shell for what's still just two pages.

- **Customers** — mirrors `prototype/business/customers.html`'s two-tab layout (Members / Following),
  built against REAL data only:
  - **Members tab**: `MembershipAppService.GetMembersAsync` (`GET /api/app/memberships`, permission
    `Eksabli.Memberships.View`) — joins this tenant's `Membership` (ambient-tenant-scoped) with
    Host-realm `CustomerProfile`/`IdentityUser` (name/phone — same cross-realm join `PosAppService
    .LookupCustomerByPhoneAsync` already used for a single customer, just batched via
    `IIdentityUserRepository.GetListByIdsAsync`) and this tenant's `PointsWallet`/`Tier`
    (balance/tier). Filters/sorts/paginates in C# after loading the tenant's full member set — same
    "acceptable at this scale" approach `AdminTenantAppService` uses. "Last Active" is `PointsWallet
    .LastModificationTime` (bumped by ABP's own auditing on a real points transaction) — the closest
    genuine signal, since there's no login/session tracking anywhere in this codebase.
  - **Following tab**: `FollowAppService.GetFollowersAsync`, enriched via `FollowerDto` (name/phone
    join, same pattern as `MemberDto`) — `FollowDto` itself stays bare for the customer-facing
    `GetMyFollowsAsync`.
  - `[MISSING BACKEND CAPABILITY]`, deliberately not built: Export button (no Excel-export capability
    for Memberships); "Convert to Campaign Target" per follower (permission exists, explicitly
    "parity/future use", no endpoint backs it); a customer-details drill-down page (names are plain
    text, not a link to a page that doesn't exist). Real `MembershipStatus` is only `Active`/`Frozen`
    — the prototype's "At Risk"/"Churned" don't exist and aren't reproduced. Tier filter dropdown is
    best-effort/hidden-on-failure (`TiersController` gates its whole controller on `Eksabli.Tiers
    .Default`, a permission a `Memberships.View`-only viewer doesn't necessarily hold).
- **Employees** — mirrors `prototype/business/employees.html`. **No backend changes needed at all** —
  `EmployeeAssignmentsController` (`GetListAsync`/`InviteAsync`/`UpdateAsync`/`RemoveAsync`, whole
  controller gated on `Eksabli.EmployeeAssignments.Default`, no separate read permission) and its
  Angular proxy (`proxy/employee-assignments/*`, `proxy/branches/*`) already existed, fully generated,
  untouched. Real fields, deliberately different from the prototype's exact shape:
  - **No "Name" column** — same finding as the Users page: `IdentityUser.Name`/`.Surname` are never
    populated for a staff account anywhere in this codebase. The identity cell shows the real email
    directly instead of a fabricated name or a redundant separate email column.
  - **No "Status" column** — the prototype's Active/Invited/Suspended aren't real: `InviteAsync`
    creates a fully active account immediately (no pending-invite state exists, same finding as Users),
    and there's no soft-suspend — `RemoveAsync` deletes the `EmployeeAssignment` row outright, which is
    also the exact real mechanism that revokes POS access (`PosAppService`'s staff-role check reads
    this same table — confirmed by reading it). Every listed row is active by construction; a static
    badge with no real variation was omitted rather than shown as decoration.
  - **Branch** resolves via a real bulk `BranchesService.getList` lookup (best-effort, same pattern
    used everywhere); a null `BranchId` is a real documented domain state
    (`EmployeeAssignment.BranchId`'s own comment: "null = access to all branches") shown as "All
    branches", not treated as missing data.
  - Invite modal's Role dropdown excludes "Owner" (matches the prototype) — there's no invite-as-owner
    flow, `EmployeeRole.Owner` is only ever set once, at business registration.
  - The Owner's own row never shows a Revoke action (matches the prototype's own real safeguard).
  - `[MISSING BACKEND CAPABILITY]`: no search/filter box (`PagedAndSortedResultRequestDto` has no
    `filterText` for this endpoint, same gap as the Support Tickets queue); no edit-role/reassign-branch
    UI even though `UpdateAsync` exists server-side — the prototype itself doesn't expose one either, so
    this matches prototype scope rather than being a gap introduced here.
- **Dashboard** (`business/dashboard/`) — mirrors `prototype/business/dashboard.html`, and is now the
  landing page for `/business` (bare path redirects here, matching `redirectAuthenticatedToHomeGuard`'s
  destination for a business-realm account — see the top of this file). Built entirely from a **real,
  already-implemented, purpose-built endpoint that neither this session nor any Angular work created**:
  `IReportsAppService.GetDashboardHomeAsync()` (`GET /api/app/report/dashboard-home`, whole
  `ReportsController` gated on `Eksabli.Reports.Default`) — its own code comment says it "centralizes
  KPI calculation so the definitions in docs/eksabli-loyalty-platform/features/07-business-dashboard/
  README.md#business-rules stay the single source of truth," confirming Feature 07 (Business Dashboard)
  was fully built server-side well before any Angular work started here. `proxy/reports/*` /
  `proxy/controllers/reports.service.ts` were also already fully generated — **no backend changes
  needed** for this page either.
  - **Stat tiles** (all real, `DashboardHomeDto`): Active Members (last-30-days Earn-transaction
    activity), Points Issued/Redeemed (30d sums), Active Campaigns (yes — the `Campaign` entity and its
    Active status genuinely exist server-side even though no Campaigns *page* exists anywhere yet; the
    count is real, just not linked to anything). No fake "+X%" deltas (the DTO has none) except "X% of
    issued" under Points Redeemed, which IS real — computed client-side from the same two already-
    fetched sums, not invented.
  - **Low-stock alert banner** — real, from `DashboardHomeDto.LowStockRewards` (`Reward
    .StockRemaining <= 10`, the same threshold the backend itself uses). Not linked anywhere (no
    Rewards page exists yet).
  - **Member Growth chart (7 months) + "+X% MoM" badge** — both real. `IReportsAppService
    .GetMemberGrowthAsync` returns per-DAY new-member counts for an arbitrary range, not pre-bucketed
    by month, so `business-dashboard.component.ts` fetches one 7-month window and re-buckets
    client-side (`bucketByMonth()`), filling zero-count months rather than skipping them. The MoM badge
    is a real last-vs-prior-month delta computed from that same data, hidden (not shown as 0%/∞) when
    not computable (fewer than 2 months of data, or prior month was 0).
  - **Quick Actions** — only 2 links, both to pages that actually exist (`/business/customers`,
    `/business/employees`). The prototype's own "Award points (POS)"/"Create a campaign"/"Add a
    branch" links were dropped — none of those pages exist in this Business Portal yet, and a link to
    a nonexistent page is a dead end, not a shortcut.
  - `[MISSING BACKEND CAPABILITY]`, deliberately not built: the prototype's "Recent Activity" table (5
    most recent transactions, any customer). No JSON list endpoint exposes tenant-wide recent
    transactions — `IWalletAppService.GetMyTransactionHistoryAsync` is scoped to the calling customer's
    own wallet only; `ReportsAppService`'s only tenant-wide transaction read is
    `GetTransactionsAsExcelFileAsync` (a bulk Excel *export*, gated on `Eksabli.Reports.Export` — see
    CLAUDE.md's Excel-export pattern), not a live list. A real "Export Transactions" action wired to
    that existing token-gated download flow would be a reasonable, low-effort follow-up — not attempted
    this pass, to keep this page's first cut scoped to what the prototype's *own* dashboard-home data
    actually needs.
- **Analytics** (`business/analytics/`) — mirrors `prototype/business/analytics.html`, built from real
  `IReportsAppService` endpoints Dashboard itself didn't use (same `Eksabli.Reports.Default` gate).
  **No backend changes needed** — every endpoint and its proxy already existed.
  - **Member Growth** (7 months) — same real `GetMemberGrowthAsync` + client-side month-bucketing as
    Dashboard, duplicated rather than extracted into a shared service (matches this codebase's existing
    per-page-owns-its-helpers convention).
  - **Redemption Rate Trend** (7 months) — real, but needed 7 separate calls to
    `GetRedemptionRateAsync` (one per month), fired together via `forkJoin` — that endpoint returns one
    aggregate rate for whatever `{from, to}` range you give it, no lower-granularity data, so a genuine
    month-by-month trend has no single-call shape. This is a real "N genuinely different date-range
    queries" case, not the "redundant concurrent calls" anti-pattern from earlier this session.
  - **"Redemptions by Branch"** — real via `GetBranchComparisonAsync`, but **relabeled** from the
    prototype's own "Active members by branch": that's not what `BranchComparisonDto.RedemptionCount`
    actually measures (coupon-redemption counts per branch — `Membership` itself isn't branch-scoped
    anywhere in the domain, so "active members by branch" isn't a real, computable thing at all).
    Labeled for what the real data actually is, not what the prototype's copy claimed.
  - **Tier Distribution** — real via `GetTierDistributionAsync`, a snapshot (no date range).
  - **KPI Definitions** — only 2 of the prototype's 4 terms, the ones with verifiable real backend
    logic behind them ("Active member", "Redemption rate"). Dropped "Churn" (a real `Churned` segment
    IS computed by `GetCustomerSegmentsAsync`, but with different real boundaries than the prototype's
    stated "60+ days" — not shown since this page doesn't surface that endpoint) and "MRR" (no endpoint
    exposes a business's own subscription cost as "MRR" anywhere this session found).
  - **Deliberate scope reduction**: no 7D/30D/90D range toggle (the prototype's own toggle would need
    to re-drive two of the four widgets differently per range while the other two stay fixed-shape
    either way) — fixed at trailing-30-days for branch comparison, trailing-7-months for both trend
    charts, matching Dashboard's own framing. A real toggle is a reasonable follow-up.
  - Real, unused-here endpoints worth knowing about for a future `reports.html`-style page:
    `GetCustomerSegmentsAsync` (New/Active/AtRisk/Churned counts) and `GetTopCustomersAsync`
    (lifetime-value ranking) — both map more naturally to `prototype/business/reports.html`'s
    "Customer Report" than to this Analytics page.
- **Branches** (`business/branches/`) — mirrors `prototype/business/branches.html`, built against
  `BranchAppService`'s real full CRUD (`GetListAsync`/`CreateAsync`/`UpdateAsync`, whole controller
  gated on `Eksabli.Branches.Default`; `Create`/`Edit` children gate the two actions this page
  exposes). Proxy already existed, fully generated — no backend changes needed for the CRUD.
  - **No "Status" badge** — `Branch` has no such field anywhere in the domain (confirmed by reading
    the entity); every branch that exists is implicitly active.
  - **No "Members" count per branch** — `Membership` isn't branch-scoped anywhere in the domain (same
    gap Analytics' "Redemptions by Branch" widget already hit).
  - **No "QR Code" check-in modal** — no branch check-in QR concept exists anywhere in this codebase
    (the real `WalletQrToken` flow is a *customer's own wallet* QR for staff to scan at POS — a
    different thing entirely from a branch's own printable code). The prototype's QR is a literal
    random pixel grid with nothing real behind it — dropped entirely, not faked.
  - **Opening hours** is free text into `Branch.OpeningHoursJson`, a freeform `[StringLength(2000)]`
    string column with no enforced schema (confirmed by reading `Branch.cs`/`BranchAppService`) — same
    "freeform blob" treatment as `BusinessProfile.SocialLinksJson` elsewhere; stored as whatever text
    is typed, not parsed as JSON.
  - **The plan-quota alert IS real** — `IBillingAppService.GetMyUsageAsync()`
    (`Eksabli.Billing.ManageOwn`, proxy already existed) returns the real
    `{ BranchCount, MaxBranches }` pair, computed server-side from the exact same
    `FeatureChecker`/`EksabliFeatures.MaxBranches` check `BranchAppService.CreateAsync` itself already
    enforces (that create call throws a real `UserFriendlyException` at the actual limit — not
    duplicated client-side, just surfaced via the normal error toast if it happens). Best-effort/
    hidden-on-failure for a viewer without `Billing.ManageOwn`.
  - No delete action exposed — `DeleteAsync` exists server-side (`Eksabli.Branches.Delete`) but the
    prototype itself has no delete button either; matches prototype scope.
  - No real pagination — branches are inherently plan-quota-limited to a handful, so a single bounded
    `maxResultCount: 100` load covers any real business.
- **Points Management** (`business/points/`) — mirrors `prototype/business/points-management.html`'s
  three tabs (Award Points / Point Rules / Tiers), built against `PosAppService`/`PointRuleAppService`/
  `TierAppService` — **no backend changes needed**, every endpoint and proxy already existed.
  **Genuinely different route shape from every other page**: `PosController` carries no ABP permission
  at all (`[Authorize]` only) — the real gate is a custom staff-role check *inside* `PosAppService`
  itself (`CheckStaffRoleAsync`, reads the caller's own `EmployeeAssignment.Role`), because invited
  staff hold zero ABP permission grants (only the seeded tenant Owner does — confirmed by reading that
  method's own code comment). So the `/business/points` route has **no `data.requiredPolicy`** — the
  Award tab is offered to any authenticated business-realm visitor, and a role mismatch surfaces as the
  real backend error at award time (a toast), matching how the API is actually gated. **If you add
  another page backed by `PosAppService` (manual adjust, redemption confirm), don't reach for
  `permissionGuard` — there's no policy to give it.** Point Rules/Tiers tabs DO have real permissions
  (`Eksabli.PointRules.Default`/`Eksabli.Tiers.Default`) and are shown/hidden per-tab inside the
  component instead (empty-string "always granted" nav permission, same shape as `AdminLayoutComponent`
  's `AbpAccount::MyAccount` entry).
  - **Award Points tab**: real, but reduced to one of the prototype's two identification modes —
    **Phone Lookup only** (`PosService.lookupCustomerByPhone` → `awardPointsByCustomerId`). "Scan QR"
    is dropped entirely: the real QR flow consumes a single-use token minted by the *customer's own*
    wallet app, which would need an actual camera-based scanner UI (no QR-scanning library/pattern
    exists anywhere in this app) — not something fakeable the way the prototype's own "Simulate Scan"
    button is.
  - **No live points-calculation preview** — the prototype shows a running base/tier/campaign
    breakdown as you type a sale amount. The real calculation only runs *inside* the actual award call
    (`PosAppService.ComputePointsAsync`, private) — there's no preview/simulate endpoint, and
    reimplementing that pipeline client-side would risk drifting from the real logic. Instead: click
    Award, then show the REAL `AwardPointsResultDto` the backend actually computed, after the fact.
  - **Point Rules tab**: real, but `PointRuleDto` is much thinner than the prototype's table implies —
    only `RuleType` (`PerCurrencyUnit`/`PerVisit`) and `PointsPerUnit` are real; there's no rule
    "label"/name and no Active/Inactive status anywhere on the entity. Shown as the rule type
    humanized ("Per $1 spent"/"Per visit") + the real points value. No "Add Rule" UI — the prototype's
    own button is a stub that just shows an info toast, not a built form even in the mockup, so this
    isn't a gap introduced here.
  - **Tiers tab**: real, read-only (matches the prototype's own tab, also read-only).
  - Neither Rules nor Tiers exposes Create/Edit despite both `PointRuleAppService`/`TierAppService`
    having real full CRUD server-side — deliberately not attempted this pass, same "don't build past
    prototype parity without being asked" reasoning as Employees/Branches.
  - The rounding-policy note ("fractional points always round down") is real — verified against
    `PosAppService.ComputePointsAsync`'s own `Math.Floor` behavior, not just copied from the
    prototype's own copy.
- **Rewards** (`business/rewards/`) — mirrors `prototype/business/rewards.html`, built against
  `RewardAppService`'s real full CRUD (`GetListAsync`/`CreateAsync`/`UpdateAsync`, whole controller
  gated on `Eksabli.Rewards.Default`; `Create`/`Edit` gate the two actions exposed). No backend
  changes needed. **This prototype page mapped unusually closely to the real DTO** — bilingual name,
  `Type` (`Discount`/`FreeProduct`/`GiftCard`, exactly the prototype's three options), points cost,
  and nullable stock (= unlimited) are all real, unchanged in meaning.
  - **"Require manager approval to redeem" checkbox is real** — maps to the real
    `ApprovalThresholdPoints` field; checking it sets `ApprovalThresholdPoints = pointsCost` (always
    triggers `PosAppService.ConfirmRedemptionAsync`'s own real Manager+ requirement for this reward),
    unchecking clears it to `null`.
  - **Status badge is real but *derived***, not a stored field — `Reward` has no `IsActive`/`Status`
    column; "Active"/"Scheduled"/"Expired" is computed client-side from the real `ValidFrom`/`ValidTo`
    bounds against the current time. "Low stock" (`StockRemaining < 10`, the same threshold
    `ReportsAppService.GetDashboardHomeAsync` itself uses) takes visual priority over the date-derived
    status when both apply, matching the prototype's own either/or badge shape.
  - **No redemption-rate progress bar** — the prototype's "62% redemption rate this month" is
    hardcoded fake data with no real per-reward computation anywhere in this codebase; dropped.
  - **No reward image** — no blob-upload UI/pattern exists anywhere in this app; a generic per-type
    icon is shown instead of pretending an image exists.
  - No delete action exposed — `DeleteAsync` exists server-side but the prototype has no delete
    button either.
- **Coupons** (`business/coupons/`) — mirrors `prototype/business/coupons.html`'s audit trail, built
  against `CouponAuditAppService` (`GetListAsync`/`GetDownloadTokenAsync`/`GetListAsExcelFileAsync`,
  whole controller gated on `Eksabli.Rewards.Default` — same permission as Rewards, since a coupon
  audit trail is really "Rewards" data). No backend changes needed.
  - Code, Status (`Issued`/`Redeemed`/`Expired`/`Cancelled` — exactly the prototype's own four
    options), and Reward name are all real; `CouponDto.RewardNameEn`/`.RewardNameAr` are already
    resolved server-side by `GetListAsync` itself.
  - **Member/Branch/"Redeemed By" are resolved client-side**, best-effort, via bulk lookups reusing
    *already-existing* endpoints — no new backend calls invented: `MembershipsService.getMembers()`
    (same call Customers' Members tab makes) for `membershipId -> name`; `BranchesService.getList()`
    for `redeemedBranchId -> name`; `EmployeeAssignmentsService.getList()` for `redeemedByEmployeeId
    -> userEmail` (`RedeemedByEmployeeId` is a raw `IdentityUser.Id`, confirmed by reading
    `PosAppService.ConfirmRedemptionAsync`'s own `CurrentUser.GetId()` call — matched against
    `EmployeeAssignmentDto.userId`, not its own assignment id).
  - Date column shows `RedeemedAt` when present, else `IssuedAt` — both real, picking whichever
    actually happened rather than the prototype's own flattened single date.
  - Status/Branch filters are real, server-side (`CouponAuditFilterDto`). **No code search** —
    `[MISSING BACKEND CAPABILITY]`, no `filterText` field, same gap as Support Tickets/Employees.
  - **Export IS real and was actually built** — the same two-step token-gated Excel-export pattern
    documented in CLAUDE.md (`GetDownloadTokenAsync()` → `GetListAsExcelFileAsync()`). This is the
    **first real `Blob`/`ObjectURL` file download implemented in this Angular app this session** — a
    plain `URL.createObjectURL(blob)` + anchor-click helper, no shared download utility existed yet to
    reuse (and none was extracted for just this one usage). The prototype's own fake "Recent Exports"
    history table (`reports.html`) is deliberately NOT replicated — there's no server-side "generated
    report" persistence anywhere; each export is generated fresh, nothing to list as history.
- **Campaigns** (`business/campaigns/`) — mirrors `prototype/business/campaigns.html`, built against
  `CampaignAppService`'s real CRUD + `ActivateAsync`/`PreviewTargetSegmentAsync`, plus
  `IReportsAppService.GetCampaignPerformanceAsync` for real per-campaign stats. No backend changes
  needed — this included discovering (by reading `Eksabli.Domain/Campaigns/CampaignRules.cs` and
  `CampaignSegmentParameters.cs`, both real C# classes with their own `Parse()` methods) the *exact*
  real JSON schemas behind `Campaign.RulesJson`/`CampaignTargetRule.ParametersJson`, rather than
  guessing at freeform text fields the way `OpeningHoursJson`/`SocialLinksJson` were treated elsewhere.
  - **Status** (`Draft`/`Active`/`Ended`) and **Type** (`Birthday`/`DoublePoints`/`SpendXGetY`/
    `WinBack`/`Vip`/`NewCustomer`/`Referral`, all 7) match the prototype's own options exactly.
  - **The wizard's Step 3 genuinely differs from the prototype's own flow, for a real reason**:
    `PreviewTargetSegmentAsync` takes an *existing campaign id* — there's no "preview before saving"
    endpoint. So the wizard actually **creates the campaign as `Draft`** at the end of Step 2, THEN
    calls the real preview for that new id, THEN Step 3 offers "Activate Now" or "Save as Draft" (it's
    already saved either way). This is the honest real flow, not the prototype's implied sequence.
  - **Step 1's rule fields are conditional on `Type`, using the real `CampaignRules` schema**:
    `DoublePoints` → `Multiplier`; `SpendXGetY` → `SpendThreshold`+`BonusPoints`; `Birthday` →
    `DaysBefore`+`BonusPoints`; `WinBack`/`Vip`/`NewCustomer` → `BonusPoints`; `Referral` has no real
    evaluator yet (confirmed in `CampaignSegmentEvaluator`'s own comment: "defined for schema parity...
    no evaluator yet") — no rule fields shown for it, with an honest note instead of pretending it does
    something.
  - **Step 2 (Target Segment) is skipped entirely for `Birthday` campaigns** —
    `CampaignSegmentEvaluator.EvaluateAsync` branches specially for `Birthday`, using Step 1's
    `DaysBefore` against date-of-birth, and ignores `TargetRules` completely (confirmed by reading that
    method). Only ONE target rule is captured, not the DTO's technically-supported list — matches the
    prototype's own single-segment dropdown. Segment param fields use the real `CampaignSegmentParameters`
    schema: `Tier` → `TierId` (a real `TiersService.getList()` dropdown); `Inactive` →
    `InactiveDays`; `NewCustomer` → `WithinDays`; `All` → no params.
  - **Per-campaign stats are real**, from `GetCampaignPerformanceAsync` (Sent / Rewarded Members /
    Bonus Points Awarded) — replacing the prototype's own Sent/Opened/Redeemed (no "opened" read-
    receipt tracking exists, and "redeemed" isn't a real per-campaign concept the same way). Only
    fetched for non-`Draft` campaigns.
  - **Real plan-quota enforcement exists** (`EksabliFeatures.MaxCampaigns`, same "throws a real
    `UserFriendlyException` at the limit" shape as Branches' `MaxBranches`) but — unlike Branches —
    there's no dedicated usage-check endpoint (`GetMyUsageAsync` only returns branch counts), so no
    proactive quota banner was built; the real error just surfaces via the normal toast if hit.
  - **No Edit/Delete UI** — both exist server-side, but re-deriving the wizard's full state (type-
    specific rules + segment params) from an existing campaign is meaningfully more work than this
    first cut; not attempted, same "not attempted this pass" reasoning as Employees/Points
    Management/Rewards. **No campaign "description" field** — `CampaignDto` has no such property (only
    bilingual name); the prototype's own description text is dropped, not fabricated.

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

**For Business Portal pages beyond Dashboard/Analytics/Customers/Employees/Branches/Points
Management/Rewards/Coupons/Campaigns** (mounted under `/business/*`, inside `BusinessLayoutComponent`,
gated on `businessRealmGuard` — see the top of this file) — no equivalent implementation-plan doc
exists yet (the backend-readiness doc covers Host-realm Admin Portal features only); scope has been
driven directly by `prototype/business/*.html` + backend-reality-checking this session. Natural next
pages, if asked for: check `prototype/business/*.html` for what exists (offers, settings,
notifications, transactions, subscription/billing all have prototype pages — verify each against the
REAL backend the same way this session did for the nine pages already built, and give each its own
real permission as `data.requiredPolicy` on a `/business/*` child route + a `BusinessLayoutComponent`
nav entry — unless the underlying app service turns out to have no ABP permission at all (like Points
Management), in which case don't force one). A reusable `downloadBlob()`-style helper is worth
extracting into `shared/` the next time a second page needs a file download — `business-coupons
.component.ts` currently has the only copy. `NotificationAppService` and `Eksabli.Notifications`
(channel/delivery-rate concepts already surfaced via `IReportsAppService
.GetNotificationDeliveryRatesAsync`, unused so far) are real and worth checking against
`notifications.html` next — Campaigns' own real `NotificationsSent`/`Queued`/`Failed` stats
(`CampaignPerformanceDto`) suggest the underlying Notification entity/pipeline is genuinely built out.
**`prototype/business/reports.html` was checked and found to be a weak next-candidate** — unlike
`analytics.html` (built this session), its "Generate report as CSV/PDF" buttons and "Recent Exports"
history table have no real backend behind them (`ReportsAppService`'s only true report-generation
capability is the single Excel export already noted as a follow-up under Dashboard's own bullet above
— no CSV/PDF, no saved-report/export-history persistence exists anywhere). Building it for real would
mean a much smaller page than the prototype shows (basically just an Export Transactions button); don't
build it expecting the prototype's full shape to be achievable. `IReportsAppService.
GetCustomerSegmentsAsync` (New/Active/AtRisk/Churned) and `GetTopCustomersAsync` (lifetime-value
ranking) are still real and unused if a scaled-down customer-report-style widget is ever wanted.
`TierAppService`, `PointRuleAppService`, `WalletAppService` were referenced earlier this session but
not opened — read
the actual service before assuming a page's data is available.

## Useful memory

This project also has persistent cross-session memory (separate from this file) with notes like
"repo's own status docs are stale, verify backend status via git log + src/" and the user's
preference for direct execution without process check-ins — those are already reflected in the
rules above, just noting they exist in case you're wondering why certain phrasing shows up as
settled fact.
