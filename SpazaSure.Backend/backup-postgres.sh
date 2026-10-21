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

# The off-site copy needs rsync and ssh, which the postgres image doesn't include.
if [ -n "${OFFSITE_TARGET:-}" ] && ! command -v rsync >/dev/null 2>&1; then
  apk add --no-cache rsync openssh-client >/dev/null || echo "Could not install rsync; off-site copies will fail." >&2
fi

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

  # Uploaded files (documents, report photos, product images) live outside the database.
  for dir in ${UPLOADS_DIRS:-}; do
    if [ -d "$dir" ]; then
      upload_archive="/backups/uploads-$(basename "$dir")-${timestamp}.tar.gz"
      if tar -czf "${upload_archive}.tmp" -C "$dir" . && mv "${upload_archive}.tmp" "$upload_archive"; then
        echo "Uploads backed up: $upload_archive"
      else
        rm -f "${upload_archive}.tmp"
        echo "Uploads backup failed for $dir" >&2
      fi
    fi
  done
  find /backups -maxdepth 1 -type f -name "uploads-*.tar.gz" \
    -mtime "+${BACKUP_RETENTION_DAYS}" -delete

  # Off-site copy (e.g. a Hetzner Storage Box), so losing this server doesn't lose the backups.
  # Copies only; it never deletes off-site files, so wiping this server can't wipe them too.
  if [ -n "${OFFSITE_TARGET:-}" ]; then
    key_file="${OFFSITE_SSH_KEY_FILE:-/backups/offsite_key}"
    if [ ! -r "$key_file" ]; then
      echo "Off-site copy skipped: SSH key $key_file not found." >&2
    elif rsync -a --ignore-existing \
        -e "ssh -p ${OFFSITE_SSH_PORT:-23} -i $key_file -o StrictHostKeyChecking=accept-new -o UserKnownHostsFile=/backups/.known_hosts" \
        --include="${PGDATABASE}-*.dump" --include="uploads-*.tar.gz" --exclude="*" \
        /backups/ "${OFFSITE_TARGET%/}/"; then
      echo "Off-site copy succeeded: $OFFSITE_TARGET"
    else
      echo "Off-site copy to $OFFSITE_TARGET failed." >&2
    fi
  fi

  trap - EXIT HUP INT TERM
  sleep "$BACKUP_INTERVAL_SECONDS"
done
