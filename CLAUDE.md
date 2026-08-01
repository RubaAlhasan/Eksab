# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

**Eksabli** is an ABP Framework (v10.5.0) layered DDD solution — a "book store" style app with `Author` and `Book`
entities — targeting **.NET 10** on the backend and **Angular 21** on the frontend. Database provider is
**PostgreSQL** (`Volo.Abp.EntityFrameworkCore.PostgreSql`, `Npgsql`).

There is also an `angular/.claude/CLAUDE.md` scoped to the Angular workspace with frontend-specific conventions
(signals, standalone components, `input()`/`output()`, etc.) — it applies only when working inside `angular/`.

**ABP Cursor rules** live under `.cursor/rules/` and cover ABP conventions in depth (module system, DDD patterns,
dependency direction, EF Core, testing, Angular UI, CLI commands). Consult them for canonical ABP patterns; the
notes below focus on how *this* solution actually diverges from or applies those conventions.

## Commands

### Backend (.NET)
```bash
dotnet build                                    # build the whole solution (Eksabli.slnx)
dotnet run --project src/Eksabli.HttpApi.Host    # run the API host (https://localhost:44330)
dotnet run --project src/Eksabli.DbMigrator      # apply EF Core migrations + seed data (run before first launch)

# EF Core migrations (run from the EF Core project — it has an IDesignTimeDbContextFactory, no -s needed)
cd src/Eksabli.EntityFrameworkCore
dotnet ef migrations add <Name>
dotnet ef database update                        # or just run Eksabli.DbMigrator again

# Tests
dotnet test                                       # all test projects
dotnet test test/Eksabli.Domain.Tests
dotnet test test/Eksabli.Application.Tests
dotnet test test/Eksabli.EntityFrameworkCore.Tests
dotnet test --filter "FullyQualifiedName~BookAppService_Tests.Should_Create_Book"   # single test
```

### Frontend (Angular) — run from `angular/`
```bash
npm install
abp install-libs        # from solution root; installs ABP client-side libs
npm start                # ng serve, http://localhost:4200 (requires HttpApi.Host running)
npm run build            # ng build
npm run build:prod       # production build
npm test                 # ng test (vitest)
npm run lint              # ng lint

abp generate-proxy -t ng # regenerate src/app/proxy/* from the running HttpApi.Host
```

### Certificates (first run / production)
```bash
dotnet dev-certs https -v -ep openiddict.pfx -p a8a5c4df-3387-44f7-b368-e21f3f0b2f4e
```

## Architecture

Standard ABP layered DDD dependency chain (lower → higher, arrows point "referenced by"):

```
Eksabli.Domain.Shared  →  Eksabli.Domain  →  Eksabli.Application.Contracts  →  Eksabli.Application  →  Eksabli.HttpApi  →  Eksabli.HttpApi.Host
                                    ↓                                                                          ↑
                          Eksabli.EntityFrameworkCore ──────────────────────────────────────────────────────────
Eksabli.DbMigrator depends on EntityFrameworkCore. angular/ talks to HttpApi.Host via generated proxies in src/app/proxy/.
```

- `src/Eksabli.Domain/Authors/Author.cs`, `src/Eksabli.Domain/Books/Book.cs` — the two aggregate roots.
  **Note**: unlike the "rich domain model" pattern documented in `.cursor/rules`, these entities use plain public
  setters (anemic style) rather than private setters + behavior methods. Follow the existing style for consistency
  unless asked to refactor.
- `src/Eksabli.EntityFrameworkCore/EntityFrameworkCore/EksabliDbContext.cs` — single `DbContext` for the whole app;
  it also implements `IIdentityDbContext` and `ITenantManagementDbContext` (via `[ReplaceDbContext]`) so Identity and
  Tenant Management module tables live in the same context/database. Table prefix is `"App"` (`EksabliConsts.DbTablePrefix`), no schema.
- `src/Eksabli.Application.Contracts/Permissions/` — `EksabliPermissions` + `EksabliPermissionDefinitionProvider`,
  the single source of truth for permission names (`Books.Create/Edit/Delete`, etc.) used in `[Authorize(...)]`
  attributes across app services.

### Object mapping — Mapperly, invoked via `ObjectMapper`
Mapping uses **Mapperly** (compile-time), defined in `src/Eksabli.Application/EksabliApplicationMappers.cs` as
partial classes inheriting `MapperBase<TSource, TDest>` (e.g. `EksabliBookToBookDtoMapper`). Unlike the generic
Cursor-rule example (which injects a mapper class directly), app services in this repo call ABP's generic
`ObjectMapper.Map<TSource, TDest>(...)` — the mapper classes above are picked up automatically by ABP's Mapperly
integration and never injected/called directly. Follow this convention for new entities: add a `[Mapper]` partial
class to `EksabliApplicationMappers.cs`, don't inject it.

### Excel export pattern (Books & Authors)
Both `BookAppService` and `AuthorAppService` implement a token-gated Excel download flow (see
`src/Eksabli.Application/Books/BookAppService.cs`):
1. `GetDownloadTokenAsync()` — authorized call that mints a short-lived token, cached via
   `IDistributedCache<{Entity}ExcelDownloadTokenCacheItem, string>` (30s TTL).
2. `GetListAsExcelFileAsync(...)` — `[AllowAnonymous]`, validates the token from step 1, streams an `.xlsx` via
   **MiniExcelLibs** as `IRemoteStreamContent`.

This two-step pattern exists because file downloads can't easily carry the auth header the SPA normally sends —
reuse it rather than adding `[AllowAnonymous]` directly to a data-returning endpoint.

### Testing
- `Eksabli.Domain.Tests`, `Eksabli.Application.Tests`, `Eksabli.EntityFrameworkCore.Tests` — all integration-style,
  built on ABP's test base classes (`EksabliTestBase`, per-layer test modules) with `AddAlwaysAllowAuthorization()`.
- Despite the app running on **PostgreSQL** in dev/prod, **all test projects use an in-memory SQLite connection**
  (`EksabliEntityFrameworkCoreTestModule` swaps `UseNpgsql()` for `UseSqlite(":memory:")` and creates tables
  directly via `IRelationalDatabaseCreator`). Don't assume Postgres-specific SQL/behavior is tested.
- `Eksabli.TestBase` seeds data through the standard ABP `IDataSeeder` on module init — see
  `BookStoreDataSeederContributor` in `src/Eksabli.Domain/` for what's seeded.

### ABP Studio MCP tools
If ABP Studio is open with this solution loaded, MCP tools are available for starting/stopping/monitoring the
running apps, running DB-migration/seed tasks, and viewing logs/exceptions/requests — see
`.cursor/rules/mcp-studio.mdc` for the full tool reference and workflows. Prefer `list_*` tools before acting
(e.g. `list_runnable_applications` before `start_application`).
