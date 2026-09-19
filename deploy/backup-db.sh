#!/usr/bin/env bash
#
# Nightly Postgres dump for the compose-hosted database.
#
# Install on the VPS:
#   chmod +x /opt/eksabli/deploy/backup-db.sh
#   crontab -e
#   0 3 * * * /opt/eksabli/deploy/backup-db.sh >> /var/log/eksabli-backup.log 2>&1
#
# The named `pgdata` volume is NOT a backup: it survives `docker compose down` but
# `docker compose down -v` deletes it permanently. Copying the volume directory while
# Postgres is running produces a torn, unrestorable snapshot -- always dump instead.

set -euo pipefail

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKUP_DIR="${BACKUP_DIR:-$DEPLOY_DIR/backups}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"

set -a
# shellcheck source=/dev/null
. "$DEPLOY_DIR/.env"
set +a

mkdir -p "$BACKUP_DIR"
TARGET="$BACKUP_DIR/eksabli_$(date +%F_%H%M).dump"

# pg_dump runs INSIDE the container so its version always matches the server's.
# -T is mandatory: without it `exec` allocates a TTY that mangles the binary stream
# into a dump which looks fine and only fails years later, at restore time.
# -Fc is the custom format: compressed, and it allows restoring a single table.
docker compose -f "$DEPLOY_DIR/docker-compose.yml" exec -T postgres \
  pg_dump -U "$POSTGRES_USER" -Fc "$POSTGRES_DB" > "$TARGET"

# An empty file means the dump failed; bail before rotation deletes a good one.
if [ ! -s "$TARGET" ]; then
  echo "$(date -Is) FAILED: empty dump, removing $TARGET" >&2
  rm -f "$TARGET"
  exit 1
fi

find "$BACKUP_DIR" -name 'eksabli_*.dump' -mtime +"$RETENTION_DAYS" -delete

echo "$(date -Is) OK $TARGET ($(du -h "$TARGET" | cut -f1))"
