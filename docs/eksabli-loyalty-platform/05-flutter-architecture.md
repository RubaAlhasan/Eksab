# 5. Flutter Architecture

[← Back to index](README.md)

## Contents
- [Folder structure](#folder-structure)
- [State management: Riverpod vs Bloc](#state-management-riverpod-vs-bloc)
- [Navigation](#navigation)
- [Offline & caching strategy](#offline--caching-strategy)
- [Localization](#localization)
- [Theming](#theming)
- [Dependency injection](#dependency-injection)

## Folder structure

Feature-first, not layer-first at the top level — mirrors the module boundaries already established
on the backend ([System Architecture](02-system-architecture.md#architecture-pattern-modular-monolith)),
so a feature like "Rewards" is easy to reason about (and easy to hand to one developer) end to end:

```
lib/
├── main.dart
├── app/
│   ├── app.dart                 # MaterialApp/router root
│   ├── theme/                   # light/dark ThemeData, tokens
│   └── router/                  # go_router configuration
├── core/
│   ├── network/                 # Dio client, interceptors (auth, retry), API base config
│   ├── auth/                    # token storage, refresh logic, OTP flow
│   ├── cache/                   # local DB (Drift/Isar) setup
│   ├── error/                   # failure types, exception mapping
│   └── di/                      # dependency injection setup
├── features/
│   ├── onboarding/
│   ├── auth/                    # register, login, OTP
│   ├── home/
│   ├── discovery/                # nearby stores, search
│   ├── store_profile/
│   ├── membership/               # join flow
│   ├── wallet/                   # cross-business wallet list
│   ├── points/                   # per-business points + transaction history
│   ├── rewards/                  # catalog, redemption
│   ├── campaigns/                # campaigns/offers feed
│   ├── notifications/
│   ├── referral/
│   ├── favorites/
│   └── profile/                  # profile, settings, devices
│       ├── data/                 # DTOs, API client, local cache
│       ├── domain/                # entities, repository interfaces, use cases
│       └── presentation/          # screens, widgets, providers/blocs
└── shared/
    ├── widgets/                   # buttons, cards, empty states, shared design-system pieces
    └── extensions/
```

Each `features/*` folder follows the same `data/domain/presentation` split — consistent enough to
navigate on autopilot, without forcing a heavier layered-architecture package on day one.

## State management: Riverpod vs Bloc

| | Riverpod | Bloc |
|---|---|---|
| Boilerplate | Low — providers + `AsyncNotifier`/`Notifier` | Higher — explicit events/states per feature |
| Compile-time safety | Strong (no `BuildContext`-dependent lookups, provider graph is type-checked) | Good, but event/state wiring is more manual |
| Testability | Straightforward — override providers in tests | Straightforward — well-established `bloc_test` patterns |
| Learning curve for new team members | Slightly gentler, especially coming from plain `setState`/hooks | Steeper but very prescriptive (helps consistency across a larger team) |
| Ecosystem fit with the async, cache-heavy nature of this app (wallet balances, notification streams) | Very good — `AsyncNotifier` + `ref.invalidate`/`ref.listen` map naturally onto "cached value, refresh in background" | Also fine, more ceremony for the same result |

**Recommendation: Riverpod.** Not because Bloc is wrong — both are production-proven — but Eksabli's
state shape is dominated by "cached value shown immediately, silently refreshed from network"
(wallet balances, store profiles, notification counts), which is close to Riverpod's core use case
with less boilerplate per feature. If the team already has deep Bloc experience, that's a legitimate
reason to override this — team familiarity often outweighs a marginal architectural preference.

## Navigation

**go_router**, declarative routes, deep-link support from day one (a push notification like "you
earned points at Nike" or a referral link must deep-link straight to the relevant screen, not just
open the app to Home). Route guards enforce auth state (redirect to `/login` if the session is
missing/expired) and the POS-vs-full-dashboard split isn't relevant here since that's the Angular
side — but the equivalent guard exists for e.g. gating the QR-scanner "award points" affordance,
which is a business-side concept and shouldn't appear at all in the consumer app's route tree.

## Offline & caching strategy

**Read-through cache for display, never for money.** This is the one place a naive "support full
offline mode" instinct is actively wrong for this product:

| Data | Offline behavior |
|---|---|
| Wallet balances, store profiles, transaction history, reward catalog | **Cache locally (Drift or Isar)**, render instantly from cache on screen open, refresh from network in the background, show a subtle "last updated" indicator if stale | 
| Points earning, redemption, joining a business | **Never processed offline.** These are server-authoritative mutations ([Security](02-system-architecture.md#security)) — the app should clearly disable/queue-with-explanation these actions when offline, not silently accept them and sync later | 
| QR/wallet code display | Must work with a **very recent** cached token at minimum, but redemption still requires the staff device to validate against the server in real time — an offline QR is not a valid discount | 

The reasoning: unlike a notes app, "sync later" for a points mutation means either trusting a client
device's claim about what happened while offline (fraud risk) or building a conflict-resolution
system for money-like data (real complexity for a benefit — "the app half-works with no signal" —
that a checkout counter with wifi/data essentially always has anyway).

## Localization

`flutter_localizations` + `intl`, with **`ar` and `en` ARB files both shipped in Phase 1** — the
target market is confirmed bilingual Arabic/English, not a future add-on. Structure it as a
first-class concern rather than an afterthought: every string in `features/*/presentation` goes
through localization from the first screen built, matching the [dark-mode guidance](04-product-experience.md#19-ux-guidelines) —
same "cheap now, expensive later" logic applies to both.

Flutter's `Directionality`/`TextDirection` handles RTL layout mirroring automatically when the `ar`
locale is active, but budget explicit RTL QA for the Wallet and Rewards screens specifically —
numeric/currency-heavy layouts (a balance number next to a currency label, a points-cost badge on a
reward card) are the most common place RTL bugs hide, because the numerals themselves stay
LTR-shaped even when the surrounding layout flips.

## Theming

Centralized `ThemeData` (light + dark) in `app/theme/`, token-based (colors, spacing, typography)
rather than hardcoded per-widget values, so the [dark mode requirement](04-product-experience.md#19-ux-guidelines)
and any future white-label/per-tenant-branding request (a plausible Enterprise-tier ask — see
[pricing tiers](01-business-strategy.md#revenue-model--pricing)) are both a token swap, not a
per-screen audit.

## Dependency injection

Riverpod's own provider graph doubles as the DI mechanism — no need for a second DI framework
(`get_it`, `injectable`) layered on top. Repositories and API clients are exposed as providers in
`core/di/`, features depend on the abstractions (repository interfaces in each feature's `domain/`
folder), and tests override providers with fakes. This mirrors the same "don't add a second
framework for something the primary one already does" principle applied throughout this design
(ABP Feature Management instead of a custom entitlements engine, ABP audit logging instead of a
custom one, etc.).
