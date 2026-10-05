#!/usr/bin/env bash
#
# Post-deploy assertions. Run from deploy/ after `docker compose up -d --build`:
#
#     ./verify-deploy.sh https://api.example.com/health-status
#
# Exists because both outages so far were silent: a deploy reported success while shipping
# nothing. `docker compose up` exiting 0 means the containers started, not that the right code
# is running or that the schema matches it.
#
#   * a chmod'd file aborted `git checkout`, the build reused the OLD source, and three
#     migrations went unapplied -- with a green deploy and a healthy /health-status
#   * backup-db.sh lost its exec bit and every nightly cron failed for two weeks, to a log
#     nobody reads
#
# A health check cannot catch either: the app is perfectly healthy running last month's code
# against last month's schema. So this compares the migrations ON DISK with the ones the
# database has actually applied, which is the one assertion that would have caught it.

set -uo pipefail

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(dirname "$DEPLOY_DIR")"
HEALTH_URL="${1:-}"
MIGRATIONS_DIR="$REPO_DIR/src/Eksabli.EntityFrameworkCore/Migrations"
BACKUP_MAX_AGE_HOURS="${BACKUP_MAX_AGE_HOURS:-48}"
READY_ATTEMPTS="${READY_ATTEMPTS:-30}"   # 30 x 6s = 3 minutes
READY_INTERVAL="${READY_INTERVAL:-6}"

set -a; . "$DEPLOY_DIR/.env"; set +a
compose() { docker compose -f "$DEPLOY_DIR/docker-compose.yml" "$@"; }

fail=0
note() { printf '  %s\n' "$*"; }

# --- 0. wait for the app to be ready ------------------------------------------------------
# `docker compose up -d` returns when containers START, not when the app is READY: this one
# runs migrations and seeding first and needs ~20-30s before /health-status answers. Probing
# once here failed a deploy that had in fact succeeded, so wait rather than guess.
# Readiness also gates everything below -- the migration check is only meaningful once the
# startup task that applies migrations has finished.
if [ -n "$HEALTH_URL" ]; then
  echo "== readiness =="
  ready=0
  for i in $(seq 1 "$READY_ATTEMPTS"); do
    code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 15 "$HEALTH_URL" || true)
    if [ "$code" = "200" ]; then
      note "OK: healthy after $(( (i - 1) * READY_INTERVAL ))s"
      ready=1
      break
    fi
    sleep "$READY_INTERVAL"
  done
  if [ "$ready" -ne 1 ]; then
    note "FAIL: $HEALTH_URL never returned 200 within $(( READY_ATTEMPTS * READY_INTERVAL ))s (last: $code)"
    fail=1
  fi
fi

# --- 1. every migration in the working tree is applied in the database -------------------
echo "== migrations =="
expected=$(find "$MIGRATIONS_DIR" -maxdepth 1 -name '*.cs' \
            ! -name '*.Designer.cs' ! -name '*ModelSnapshot.cs' -printf '%f\n' \
            | sed 's/\.cs$//' | sort)
applied=$(compose exec -T postgres \
            psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At \
            -c 'select "MigrationId" from "__EFMigrationsHistory"' 2>/dev/null | sort)

missing=$(comm -23 <(printf '%s\n' "$expected") <(printf '%s\n' "$applied"))
if [ -n "$missing" ]; then
  echo "  PENDING MIGRATIONS -- the running code expects a schema the database does not have:"
  printf '%s\n' "$missing" | sed 's/^/    /'
  fail=1
else
  note "OK: $(printf '%s\n' "$expected" | grep -c .) migration(s), all applied"
fi

# --- 2. the startup task swallows migration errors into a log line, so look for them -----
echo "== startup errors =="
if compose logs --since 10m api 2>/dev/null | grep -q 'migration/seed failed'; then
  note "FAIL: CreateDatabaseStartupTask logged a migration/seed failure"
  compose logs --since 10m api 2>/dev/null | grep -A4 'migration/seed failed' | head -8 | sed 's/^/    /'
  fail=1
else
  note "OK: no migration/seed failure logged"
fi

# --- 4. backups are still happening (warn only; a stale backup is not a bad deploy) ------
echo "== backups =="
newest=$(find "$DEPLOY_DIR/backups" -name 'eksabli_*.dump' -printf '%T@ %p\n' 2>/dev/null | sort -rn | head -1)
if [ ! -d "$DEPLOY_DIR/backups" ]; then
  # No backups directory at all means this stack has no backup schedule -- true of staging,
  # whose database is disposable. A warning that is always present on a healthy stack teaches
  # people to ignore it, including on production where it is the signal that matters.
  note "SKIP: no backups/ directory (stack has no backup schedule)"
elif [ -z "$newest" ]; then
  note "WARN: backups/ exists but holds no dumps -- has the cron stopped?"
else
  age_h=$(( ( $(date +%s) - ${newest%%.*} ) / 3600 ))
  if [ "$age_h" -gt "$BACKUP_MAX_AGE_HOURS" ]; then
    note "WARN: newest backup is ${age_h}h old (> ${BACKUP_MAX_AGE_HOURS}h) -- is the cron still running?"
  else
    note "OK: newest backup is ${age_h}h old"
  fi
fi

echo
[ "$fail" -eq 0 ] && echo "verify-deploy: PASS" || echo "verify-deploy: FAIL"
exit "$fail"
