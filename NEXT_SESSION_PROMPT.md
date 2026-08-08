# Paste this to start the next Claude Code session

I'm continuing work on **Eksabli**, a bilingual (Arabic + English, RTL) loyalty platform built on
ABP Framework 10.5 (.NET 10) + Angular 21, PostgreSQL. Read [`CLAUDE.md`](CLAUDE.md) at the repo
root first (real dev commands, architecture, Mapperly mapping convention, Excel-export pattern).
Then read this whole file before doing anything — it exists so you don't repeat mistakes/rediscover
things already settled.

**Backend status: done.** Features 01-08 (identity/multi-tenancy, businesses, loyalty engine,
billing/subscriptions, categories, support tickets, etc.) are fully implemented — don't assume the
backend needs building; verify capabilities by reading the actual code, not the docs (the docs
under `docs/eksabli-loyalty-platform/` describe the target design and can be stale vs. what's
actually implemented).

**Current work: building the Admin Portal Angular UI**, on branch `MD/admin-portal-frontend`,
feature by feature, backend-verified, build-verified at every step. This is what the rest of this
file is about.

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
- **Businesses** (`admin/businesses/`) — tenant list, approve/suspend actions.
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
- **Sidebar nav is grouped** (`AdminLayoutComponent`'s `ADMIN_NAV: AdminNavGroup[]`) — "Platform"
  (Businesses, Categories), "Billing" (Plans, Subscriptions), "Operations" (Support Tickets). Only
  groups with ≥1 real, built page exist — no placeholder/disabled nav entries for unbuilt features.
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

## What's NOT done — pick up here, in this order

Per the backend-readiness doc's Angular Implementation Order and the user's stated MVP scope:

1. Menu/route wiring for stock ABP modules already available via installed packages: Users
   (`@abp/ng.identity`), Roles, Feature Flags, Settings, Admin Profile — these are pre-built ABP UI,
   just need permission-gated nav entries added to `ADMIN_NAV`/`route.provider.ts`, matching the
   grouped-sidebar pattern (a "System" group, alongside the now-existing Platform/Billing/Operations
   groups).
2. **Dashboard last** — deliberately not built yet; it composes widgets from pages that need to
   exist first (Businesses + Support Tickets, both now built — Dashboard is unblocked but still
   explicitly saved for last). Do not start with Dashboard before #1 above.
3. **Backend-blocked, do not build yet**: Audit Logs (no `AbpAuditLogging` HttpApi module
   reference exists), full Payments (only the manual-record-payment flow Subscriptions already has
   exists), Business Details' Billing tab (needs `AdminSubscriptionFilterDto.TenantId`) — revisit
   only once/if the corresponding backend work lands.

## Useful memory

This project also has persistent cross-session memory (separate from this file) with notes like
"repo's own status docs are stale, verify backend status via git log + src/" and the user's
preference for direct execution without process check-ins — those are already reflected in the
rules above, just noting they exist in case you're wondering why certain phrasing shows up as
settled fact.
