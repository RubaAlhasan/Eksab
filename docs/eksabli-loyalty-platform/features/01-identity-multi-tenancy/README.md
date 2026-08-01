# Feature 01 — Identity & Multi-Tenancy

[← Back to feature index](../README.md)

## Overview

The foundation everything else depends on. Resolves the core tension in the business model:
**customers are one global identity that joins many independent businesses**, while **businesses
are standard ABP tenants** (isolated staff, branding, plan, permissions). Full reasoning:
[System Architecture → Two identity realms](../../02-system-architecture.md#two-identity-realms-the-key-decision).

- **MVP phase:** 1 (build and prove this before anything else)
- **Depends on:** nothing — this is what everything else depends on
- **Status:** design proven in principle; the integration test that empirically proves it (one
  customer, two tenants, independent balances, correct `IMultiTenant` filter behavior) has not yet
  been completed — see [Open questions](#open-questions) below.

## Domain model

| Entity | Purpose | Key fields | Notes |
|---|---|---|---|
| `Tenant` (ABP built-in) | A business | `Name` | Already available via `AbpTenantManagementDomainModule`, already a dependency in `EksabliDomainModule` |
| `BusinessProfile` | Loyalty-specific extension of `Tenant` | `TenantId` (FK, unique), `CategoryId`, `LogoBlobName`, `DescriptionAr`/`DescriptionEn`, `Website`, `SocialLinks (json)` | 1:1 with `Tenant`, kept separate so ABP's own tenant table stays untouched. Bilingual field pair per [Database Design → Cross-cutting notes](../../03-database-design.md#cross-cutting-notes) |
| `Branch` | A physical/logical business location | `TenantId`, `Name`, `Address`, `Latitude`, `Longitude`, `Phone`, `OpeningHours (json)` | `IMultiTenant`. Geo index now, PostGIS later ([Scalability](../../02-system-architecture.md#scalability)) |
| `IdentityUser` (ABP built-in) | Both customers (Host realm, `TenantId = null`) and staff (Tenant realm) | — | Same table, two realms distinguished purely by `TenantId` |
| `CustomerProfile` | Loyalty-specific fields for a Host-realm user | `UserId` (FK, unique), `FirstNameAr`/`FirstNameEn`(optional bilingual), `LastName`, `DateOfBirth`, `Gender` | 1:1 with `IdentityUser` where `TenantId IS NULL` |
| `EmployeeAssignment` | Which branch(es) + role a tenant-realm staff user has | `UserId`, `TenantId`, `BranchId` (nullable = all branches), `Role` | `(TenantId, UserId)` index |
| `Device` | Push target + per-device logout | `CustomerId`, `Platform`, `PushToken`, `LastActiveAt`, `AppVersion` | Unique on `PushToken` |

Full ERD: [Database Design → Identity & business core](../../03-database-design.md#identity--business-core).

## Business rules

- **Host realm = customers + platform admins. Tenant realm = businesses.** A customer's
  `IdentityUser.TenantId` is always `null`. This is the one rule every other feature assumes is true.
- **Tenant-scoped reads/writes** (staff working inside their business) rely on ABP's automatic
  `IMultiTenant` filter — never manually filter by `TenantId` in application code.
- **Cross-tenant customer reads** (a customer's own "all my businesses" view) explicitly disable the
  filter and substitute a `CustomerId` filter: `using (DataFilter.Disable<IMultiTenant>()) { ... }`.
  Never expose a general cross-tenant search to tenant-side staff — see the
  [phone-lookup scoping rule](../../02-system-architecture.md#high-level-architecture) for the
  concrete failure mode this guards against.
- **New tenant creation code does not exist yet anywhere in the repo** — confirmed by direct
  inspection; `EksabliDbMigrationService` only reads pre-existing tenants. This feature is what
  introduces it (via ABP's `TenantManager` + `ITenantRepository`).
- Auth: OpenIddict, Authorization Code + PKCE for Flutter/web (not Resource Owner Password
  Credentials). OTP login via a custom OpenIddict grant backed by a short-lived, single-use
  Redis-cached code — same shape as the download-token pattern already in this repo's
  `BookAppService`/`AuthorAppService`.

## API surface

| Group | Resources | Realm |
|---|---|---|
| `/api/identity/*` | registration, login, OTP request/verify, token refresh, profile | Host (customers) + Tenant (staff), same shape |
| `/api/businesses/*` | business search/discovery (public read), business profile, branches (tenant-authenticated write) | Mixed |

## Screens

**Flutter:** Splash, Onboarding, Register, OTP Verify, Login, Profile, Settings (device list / log
out per device). See [Product Experience → Mobile screen inventory](../../04-product-experience.md#5-mobile-app-screen-inventory).

**Angular (business dashboard):** Business sign-up/onboarding (name, category, logo, first branch),
Branches management, Employees management (invite staff, assign role + branch). See
[Dashboards & Admin → Business dashboard](../../06-dashboards-admin.md#6-business-dashboard-tenant-realm).

No mockup built yet for this feature — the mockups so far cover Home, POS award-points, and
Rewards (all feature 02/03). This feature's screens are the natural next mockup candidates.

## Permissions

Base identity/tenant management uses ABP's built-in permission groups. This feature additionally
defines the platform's own role vocabulary (not code — a naming convention for later permission
definitions): Business Owner, Branch Manager, Cashier/Redemption Staff, Marketing Manager (tenant
realm); Super Admin, Support Agent, Billing Admin, Content Moderator (host realm). Full table:
[Business Strategy → User & role model](../../01-business-strategy.md#user--role-model).

## Implementation checklist

Following this repo's own [ABP new-feature flow](../../../.cursor/rules/framework/common/development-flow.mdc),
applied to this feature:

- [ ] `BusinessProfile`, `Branch`, `CustomerProfile`, `EmployeeAssignment`, `Device` entities in
      `src/Eksabli.Domain/` (new folders, e.g. `Businesses/`, `Branches/`, `Customers/`, `Devices/`)
- [ ] Constants (`BusinessProfileConsts`, `BranchConsts`, etc.) in `src/Eksabli.Domain.Shared/`
- [ ] EF Core: `DbSet`s + `OnModelCreating` config in `EksabliDbContext`, remembering `IMultiTenant`
      on `Branch`/`EmployeeAssignment` but not on `CustomerProfile`/`Device` (Host-realm entities)
- [ ] Migration: `dotnet ef migrations add Added_BusinessIdentity` from `src/Eksabli.EntityFrameworkCore`
- [ ] Tenant-creation service (new — doesn't exist yet): wraps `TenantManager.CreateAsync` +
      `ITenantRepository.InsertAsync`
- [ ] DTOs + service interfaces in `Eksabli.Application.Contracts`, implementations in `Eksabli.Application`
- [ ] Mapperly mappers registered in `EksabliApplicationMappers.cs` (existing convention — don't
      inject the mapper class, call `ObjectMapper.Map<T,U>()`)
- [ ] Localization keys in `src/Eksabli.Domain.Shared/Localization/Eksabli/{en,ar}.json`
- [ ] Permission definitions for the role vocabulary above
- [ ] **The proof test**: one Host-realm customer, two tenants, independent state, correct
      `IMultiTenant` isolation in both directions — see Open questions

## Open questions

- **The identity-realm spike itself was started but interrupted mid-session** (design was validated
  against this repo's actual conventions — rich-model entity via `AuditedAggregateRoot<Guid>`,
  `TenantId` framework-populated via `CurrentTenant.Change()` rather than set in the constructor,
  test mirroring `SampleRepositoryTests.cs`'s direct-inheritance shape) but the entity, migration,
  and test were never actually written to disk. This is the single highest-priority next backend
  task — see the risk register entry in [Business Strategy §21](../../01-business-strategy.md#21-risks).
- Exact ABP API surface for tenant creation (`TenantManager` vs. a possible `ITenantManager`
  interface) should be confirmed against the installed `Volo.Abp.TenantManagement` package version
  via the compiler, not assumed.
