#!/bin/sh
set -eu

: "${PGHOST:?PGHOST is required}"
: "${PGPORT:=5432}"
: "${PGDATABASE:?PGDATABASE is required}"
: "${PGUSER:?PGUSER is required}"
: "${PGPASSWORD:?PGPASSWORD is required}"
: "${BACKUP_RETENTION_DAYS:=14}"
: "${BACKUP_INTERVAL_SECONDS:=86400}"

case "$BACKUP_RETENTION_DAYS" in
  ''|*[!0-9]*|0) echo "BACKUP_RETENTION_DAYS must be a positive integer." >&2; exit 1 ;;
esac
case "$BACKUP_INTERVAL_SECONDS" in
  ''|*[!0-9]*|0) echo "BACKUP_INTERVAL_SECONDS must be a positive integer." >&2; exit 1 ;;
esac

umask 077
mkdir -p /backups

while :; do
  timestamp=$(date -u +%Y%m%dT%H%M%SZ)
  archive="/backups/${PGDATABASE}-${timestamp}.dump"
  temporary="${archive}.tmp"
  restore_database="backup_check_${timestamp}_$$"

  cleanup() {
    dropdb --if-exists --maintenance-db=postgres "$restore_database" >/dev/null 2>&1 || true
    rm -f "$temporary"
  }
  trap cleanup EXIT HUP INT TERM

  echo "Creating PostgreSQL backup: $archive"
  pg_dump --format=custom --no-owner --file="$temporary"

  echo "Verifying backup by restoring into temporary database: $restore_database"
  createdb --maintenance-db=postgres --template=template0 "$restore_database"
  pg_restore --exit-on-error --no-owner --dbname="$restore_database" "$temporary"

  table_count=$(psql --dbname="$restore_database" --tuples-only --no-align \
    --command="SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'")
  if [ "$table_count" -le 0 ]; then
    echo "Backup restore check found no public tables." >&2
    exit 1
  fi

  dropdb --maintenance-db=postgres "$restore_database"
  restore_database=""
  mv "$temporary" "$archive"
  find /backups -maxdepth 1 -type f -name "${PGDATABASE}-*.dump" \
    -mtime "+${BACKUP_RETENTION_DAYS}" -delete
  echo "Backup and restore check succeeded: $archive ($table_count public tables)"

  trap - EXIT HUP INT TERM
  sleep "$BACKUP_INTERVAL_SECONDS"
done
