# Paste this to start the next Claude Code session

I'm continuing work on **Eksabli**, a loyalty platform (customers install one app, join
unlimited businesses, each with an independent points balance) built on top of an ABP Framework
10.5 (.NET 10) + Angular 21 solution currently scaffolded with a tutorial Book/Author domain.
Product/target market is confirmed **bilingual Arabic + English (RTL)**. Read
[`CLAUDE.md`](CLAUDE.md) at the repo root first — it has the real dev commands and architecture
notes. Then read this whole file before doing anything — it exists specifically so you don't
repeat mistakes I already got corrected on last session.

## What already exists

**Planning docs** — `docs/eksabli-loyalty-platform/`: 8 documents (business strategy, system
architecture, database design, product experience, Flutter architecture, dashboards/admin,
loyalty engine) plus `features/` — the same decisions re-sliced into 8 implementation-ready
vertical feature folders (`01-identity-multi-tenancy` through `08-admin-panel`), each with domain
model, API surface, screens, permissions, and a build checklist. Start any real feature work by
reading the matching folder there, not by re-deriving the design.

**Working mockup artifacts** (published, viewable, may or may not still be live depending on
session):
- `https://claude.ai/code/artifact/1bd0c7e1-9f64-4530-b4a1-fa694accd049` — customer app Home
  screen (wallet carousel, campaign banners with a live countdown-free badge), the cashier
  "Award Points" POS screen (QR-scan/phone-lookup toggle), and Rewards & Redemption (catalog →
  QR + live countdown). All phone-frame mockups, EN/AR toggle included.
- `https://claude.ai/code/artifact/5e9dc98f-d771-4454-b0e2-fc06f8c7fa8e` — marketing landing page
  mockup ("digital passbook" visual concept — perforated dividers, rubber-stamp badges, monospace
  ledger numerals). Superseded by the REAL Angular implementation below, kept for reference.

**Real Angular implementation** (this is live code, not a mockup):
- `angular/src/app/landing/` — `LandingComponent`, the actual marketing landing page, now serving
  at the app's root route `/`.
- `angular/src/app/home/home.component.*` — trimmed down to an honest placeholder (was the full
  ABP "Getting Started" tutorial page; now just a welcome message + links to Books/Authors). Lives
  at `/home`, behind `authGuard`. This is where a logged-in user lands — it is NOT a real dashboard
  yet (Feature 07 in the docs is the real one).
- `angular/src/app/footer/footer.component.ts` — was "Lepton Theme by Volosoft" boilerplate, now
  says Eksabli, localized.
- Bilingual copy for all of the above lives in `src/Eksabli.Domain.Shared/Localization/Eksabli/en.json`
  and `ar.json`, under a `Landing:`/`Dashboard:` key namespace, loaded through ABP's real
  `abpLocalization` pipe — **not** local component strings (see gotcha below).

## Conventions and gotchas learned the hard way last session — don't rediscover these

1. **Angular i18n must go through ABP's actual localization pipeline.** Add keys to
   `en.json`/`ar.json` in `src/Eksabli.Domain.Shared/Localization/Eksabli/`, reference them in
   templates as `{{ '::Namespace:Key' | abpLocalization }}` (import `LocalizationPipe`). Language
   switching calls `RouteBasedCultureUrlService.applyLanguageSelection(lang)` (the actual method
   Lepton-X's own toolbar picker uses — confirmed via the package's `.d.ts`, not the compiled
   bundle, which can be misleading). Current active language/direction: `SessionStateService.getLanguage$()`
   converted to a signal via `toSignal()`, direction via `getLocaleDirection()`. Do not invent a
   local `signal<'en'|'ar'>` + hardcoded string maps — I did that once and got corrected.

2. **Layout/chrome (sidebar+topbar vs. chrome-free) is NOT controlled by a route's own
   `data.layout` alone.** `DynamicLayoutComponent` (`@abp/ng.core`) calls `getExtractedLayout()`,
   which starts from `route.snapshot.data['layout']` but then calls `findRoute()` against the
   `RoutesService` menu tree (populated in `angular/src/app/route.provider.ts`) and lets an
   ancestor menu entry's `.layout` **override** it. `findRoute` recursively trims path segments
   and falls back to `/` if nothing else matches — so an unregistered route silently inherits
   whatever `/` resolves to. Any route needing a specific layout (`eLayoutType.empty`/`application`/`account`)
   should be registered in `route.provider.ts` with an exact-matching `path`, not just given
   `data.layout` in `app.routes.ts`. If a route isn't in the menu tree AND has no route-level
   `data.layout`, `DynamicLayoutComponent` defaults it to `empty` (this is how account/login pages
   get chrome-free rendering without any special registration).

3. **Dark mode in this app is `:host-context(.lpx-theme-dark)`**, a class Lepton-X toggles
   somewhere up the DOM — not the `prefers-color-scheme` media query. Check `home.component.scss`
   (original, pre-cleanup version if you need a reference) for the pattern.

4. **OAuth `redirectUri` is fixed** (`environment.ts` → `baseUrl`, i.e. `/`) — you can't make login
   redirect straight to `/home` by changing it without also touching the OpenIddict client's
   registered redirect URIs on the backend. Instead, `/` has a `canActivate` guard
   (`redirectAuthenticatedToHomeGuard` in `app.routes.ts`) that bounces already-authenticated
   visitors on to `/home` — landing page for anonymous visitors, dashboard placeholder for
   authenticated ones.

5. **Never put real trademarked brand names/logos on public-facing marketing material** as if
   they're customers/partners (no real ones exist pre-launch) — use fictional businesses (already
   established: Bloom & Brew, FitLine Sports, Crust & Co.). Real brand names (Starbucks, Nike,
   Pizza Shop) are fine as illustrative example data *inside internal product/app-screen mockups*
   only, because that's what the original product brief itself used as its own example — the
   distinction is "internal design tool" vs. "public collateral," not the names themselves. Also:
   no fabricated stats, testimonials, or dollar-figure pricing — pricing tiers show what's
   included, not invented amounts, since real pricing is explicitly unvalidated in the docs.

6. **Verify Angular changes with an actual build**, not just reading the diff:
   `cd angular && npx ng build --configuration development`. I was wrong twice last session about
   framework internals (the localization mechanism, the layout mechanism) before I started tracing
   actual `.d.ts`/bundle source instead of guessing from memory — trust the compiler and the real
   package source over recollection.

7. **I (the user) strongly prefer direct execution over being asked to confirm process/workflow
   questions** — if something like plan-mode state or a similar internal mechanism is ambiguous,
   just proceed with the obviously-intended work rather than pausing to ask about it. Reserve
   actual questions for genuine product/design forks where there's no clearly-correct default.

## What's NOT done — pick up here

**Top priority: the backend identity-realm spike was researched but never written.** This is the
single most important unstarted piece — see
[`docs/eksabli-loyalty-platform/features/01-identity-multi-tenancy/README.md`](docs/eksabli-loyalty-platform/features/01-identity-multi-tenancy/README.md)'s
"Open questions" section. The goal: prove one Host-realm (`TenantId = null`) customer identity can
hold independent point balances across two ABP Tenants, with `IMultiTenant` filtering behaving
correctly in both directions. Research already done (don't redo it, just implement):
- No tenant-creation code exists anywhere in the repo yet — `EksabliDbMigrationService` only reads
  existing tenants. Use ABP's `TenantManager.CreateAsync(name)` + `ITenantRepository.InsertAsync(...)`
  (both already resolvable via DI, `Volo.Abp.TenantManagement` already referenced).
- `EksabliEntityFrameworkCoreModule.cs` currently has `AddDefaultRepositories(includeAllEntities: true)`
  — the comment right above it says to remove that flag per DDD best practice (matches this repo's
  own `.cursor/rules/framework/data/ef-core.mdc`). Worth fixing alongside the new entity.
- New `Membership` entity should be rich-model (private setters, `AuditedAggregateRoot<Guid>` like
  `Book`, not anemic like `Book`/`Author` currently are — those are tutorial scaffolding being
  deleted anyway, not a pattern to propagate) with a `TenantId` that's framework-populated via
  `CurrentTenant.Change()` around inserts, never set explicitly in the constructor.
- Test belongs in `test/Eksabli.EntityFrameworkCore.Tests/EntityFrameworkCore/`, mirroring
  `SampleRepositoryTests.cs`'s direct-inheritance shape (not the abstract-generic-class pattern
  used elsewhere in this repo — that's for DB-agnostic tests, this one is inherently about the
  relational `IMultiTenant` filter).

**Also open:**
- No real business dashboard yet (Feature 07) — `/home` is a placeholder on purpose.
- Mockups only cover Home/POS/Rewards — Store Profile, full Wallet list, Search, Notifications,
  etc. aren't designed yet.
- Payment provider (Feature 04) and push/SMS/email provider (Feature 05) aren't chosen — flagged
  as open questions in those feature docs, and both should probably account for the confirmed
  Arabic/English market when decided.
- Flutter app doesn't exist in this repo — it's being built by someone else, separately (last I
  knew, they were still setting up the project). Coordinate before assuming backend API contracts
  are final.
