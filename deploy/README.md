# Backend deployment

Docker Compose stack for `Eksabli.HttpApi.Host` + PostgreSQL + nginx.

The same `docker-compose.yml` runs on a laptop and on the VPS — only `.env` changes.
Rehearse locally first; every failure mode except certificate issuance reproduces there.

| File | Purpose |
|---|---|
| `Dockerfile.api` | Multi-stage build of the API (context = repo root) |
| `docker-compose.yml` | postgres + api + nginx + one-shot certbot |
| `nginx/api.local.conf` | Plain HTTP, for rehearsal |
| `nginx/api.prod.conf` | TLS termination + HTTP→HTTPS redirect |
| `.env.example` | Every value that must change per environment |
| `backup-db.sh` | Nightly `pg_dump` with rotation |

## 1. Local rehearsal (Docker Desktop)

```bash
cd deploy
cp .env.example .env          # fill in the three CHANGE_ME values
```

Add to `C:\Windows\System32\drivers\etc\hosts` (as Administrator):

```
127.0.0.1  api.eksabli.local
127.0.0.1  app.eksabli.local
```

Generate the signing certificate the API needs in Production:

```bash
openssl req -x509 -newkey rsa:2048 -nodes -days 3650 -subj "/CN=Eksabli" \
  -keyout /tmp/k.pem -out /tmp/c.pem
openssl pkcs12 -export -out secrets/openiddict.pfx -inkey /tmp/k.pem -in /tmp/c.pem \
  -passout pass:THE_VALUE_OF_OPENIDDICT_PFX_PASSWORD
rm /tmp/k.pem /tmp/c.pem
```

Long validity is deliberate: when this certificate expires, OpenIddict fails to start and
every login stops. Put the renewal date in a calendar.

```bash
docker compose up -d --build
docker compose logs -f api
```

The first build takes a while (NuGet restore). It needs no Node toolchain and no npm
registry access: `wwwroot/libs` is committed to the repo rather than restored at build time.

**Verify in this order** — each step proves a different thing:

1. `curl http://api.eksabli.local/health-status` → healthy. Proves nginx→api routing and
   that the database migrated.
2. Check the logs for the migration/seed block. `CreateDatabaseStartupTask` catches and
   **logs** exceptions rather than crashing, so a failed migration leaves you with a
   container that looks up but has no schema. Never skip this.
3. Open `http://api.eksabli.local/swagger`, click Authorize, log in as
   `admin` / `1q2w3E*`. This is the real test: it exercises the MVC login page (proving
   the committed `wwwroot/libs` assets are present), the production certificate, and the
   forwarded-proto handling
   all at once. If Swagger's Authorize button completes the round trip, the auth stack is
   correct.

> **Change the admin password the moment the VPS is reachable.** `SeedService` creates the
> `admin` account with `EksabliConsts.AdminPasswordDefaultValue` = `1q2w3E*`, which is the
> stock ABP template password and the first thing anyone scanning your domain will try.
> Seeding is guarded on "does it already exist", so changing it is permanent — later boots
> will not reset it.

## 2. Hostinger VPS

Provision KVM 2 or better with the Ubuntu + Docker template, add your SSH key, and in the
hPanel firewall allow **only** 22, 80, 443. Point an A record for `api.yourdomain.com` at
the VPS IP and let it propagate before continuing.

```bash
ssh root@<vps-ip>
git clone <your-repo> /opt/eksabli
cd /opt/eksabli/deploy
cp .env.example .env      # production URLs, but leave NGINX_CONF on api.local.conf for now
```

Regenerate `secrets/openiddict.pfx` on the VPS with the commands from step 1 — a fresh
keypair, not the one from your laptop.

### If the host already runs a reverse proxy (Hostinger Docker template)

Hostinger's Docker template runs **Traefik** on the host network, holding :80 and :443. It
discovers containers through the Docker provider (`exposedbydefault=false`, so only labelled
ones are routed), issues and renews Let's Encrypt certificates itself, and redirects HTTP to
HTTPS. Starting this stack's own nginx there fails with `address already in use`.

On such a host, nothing extra is needed: the `api` service carries `traefik.*` labels and is
routed automatically. Set `.env` to the real hostname with `AUTH_REQUIRE_HTTPS=true` and run:

```bash
docker compose up -d --build
```

Traefik requests the certificate on first request to that hostname. It also sets
`X-Forwarded-Proto=https` and proxies WebSockets (the SignalR hub) natively, so the nginx
config's manual `Upgrade`/`Connection` handling is not needed.

The nginx + certbot path below is for hosts with **no** existing reverse proxy, and must be
started explicitly with `docker compose --profile nginx up -d`.

### Issuing the certificate (nginx profile only)

Order matters. `api.prod.conf` references certificate files that do not exist yet, so nginx
will refuse to start if you enable it too early.

```bash
docker compose up -d --build        # still on api.local.conf; serves the ACME challenge
docker compose run --rm certbot certonly --webroot -w /var/www/certbot \
  -d api.yourdomain.com --email you@yourdomain.com --agree-tos --no-eff-email

sed -i 's|api.local.conf|api.prod.conf|' .env
docker compose up -d                # nginx picks up the TLS config
```

Renewal, via cron:

```
0 4 * * 1 cd /opt/eksabli/deploy && docker compose run --rm certbot renew --webroot -w /var/www/certbot --quiet && docker compose restart nginx
```

Then repeat the three verification steps from section 1 against the real domain.

### Settings that must differ between rehearsal and production

Two flags in `.env` are not cosmetic — leaving the rehearsal values in production weakens
the deployment:

| | Rehearsal | Production | Why |
|---|---|---|---|
| `AUTH_REQUIRE_HTTPS` | `false` | `true` | Rehearsal is plain HTTP, so OpenIddict's transport-security check has to be relaxed. In production it must stay on, or plain-HTTP token requests are accepted. |
| `APP_DISABLE_PII` | `false` | `true` | Controls `IdentityModelEventSource.ShowPII` / `LogCompleteSecurityArtifact` — with PII enabled, tokens and user identifiers are written to `Logs/logs.txt` in clear. |

Production can keep `AUTH_REQUIRE_HTTPS=true` because `EksabliHttpApiHostModule` now configures
`ForwardedHeadersOptions` unconditionally. That configuration previously sat inside the
`if (!RequireHttpsMetadata)` branch, so reading `X-Forwarded-Proto` and disabling the
transport-security check were the same switch — a proxied deployment had to give up the check
just to be told it was behind TLS. They are now independent.

If you ever see `"this server only accepts HTTPS requests"` from the token endpoint, the cause
is the proxy not sending `X-Forwarded-Proto`, not this flag. Check the nginx `location /` block.

## 3. Opening the database

Postgres is bound to `127.0.0.1` only, so reach it through SSH.

Quick look:

```bash
docker compose exec postgres psql -U eksabli -d Eksabli
# \dt "App"*    -- your entities share this DbContext with Identity and Tenant Management
```

From DBeaver/pgAdmin on Windows: use the client's own **SSH tunnel** tab (host `<vps-ip>`,
user `root`, your key), then point the connection at `localhost:5432`. Or open a tunnel by
hand and connect to `localhost:5433`:

```powershell
ssh -N -L 5433:127.0.0.1:5432 root@<vps-ip>
```

## 4. Backups

```bash
chmod +x backup-db.sh
crontab -e
# 0 3 * * * /opt/eksabli/deploy/backup-db.sh >> /var/log/eksabli-backup.log 2>&1
```

Restore:

```bash
docker compose stop api        # drop open connections first
docker compose exec -T postgres \
  pg_restore -U eksabli -d Eksabli --clean --if-exists < backups/eksabli_XXXX.dump
docker compose start api
```

Do this once deliberately. A backup you have never restored is a hypothesis.

Dumps sitting on the same VPS die with the VPS — pull them down regularly
(`scp root@<vps-ip>:/opt/eksabli/deploy/backups/*.dump .`). Hostinger's weekly VM snapshots
are too coarse to be your only database backup.

### What the dump covers, and what it does not

Business logos live **in the database** (`ConfigureBlobStoring` uses `UseDatabase()`), so
uploads are already inside the dump — no separate file backup needed. Audit logs are in
there too, and since the host sets `IsEnabledForGetRequests = true` that table grows fast
enough to dominate your dump size; keep an eye on it.

Two things `pg_dump` cannot save, to be stored in a password manager **once**:

- **`secrets/openiddict.pfx` and its passphrase.** Lose them and every issued token and
  stored secret becomes undecryptable; a replacement certificate logs everyone out.
- **`STRING_ENCRYPTION_PASSPHRASE`.** Restoring a dump under a different passphrase gives
  you rows that cannot be decrypted.

## 5. Redeploying

```bash
git pull && docker compose up -d --build && docker compose logs -f api
```

Migrations and seeding run automatically on startup. If you change `API_URL` or `APP_URL`,
`SeedService` rewrites the OpenIddict client's redirect URIs on the next boot — confirm with:

```sql
SELECT "ClientId", "RedirectUris" FROM "AppOpenIddictApplications";
```
