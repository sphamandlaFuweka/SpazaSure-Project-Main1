#!/bin/sh
set -eu

: "${PGHOST:?PGHOST is required}"
: "${PGPORT:=5432}"
: "${PGDATABASE:?PGDATABASE is required}"
: "${PGUSER:?PGUSER is required}"
: "${PGPASSWORD:?PGPASSWORD is required}"
: "${BACKUP_RETENTION_DAYS:=14}"
: "${BACKUP_INTERVAL_SECONDS:=86400}"
: "${OFFSITE_AGE_RECIPIENT:=}"

case "$BACKUP_RETENTION_DAYS" in
  ''|*[!0-9]*|0) echo "BACKUP_RETENTION_DAYS must be a positive integer." >&2; exit 1 ;;
esac
case "$BACKUP_INTERVAL_SECONDS" in
  ''|*[!0-9]*|0) echo "BACKUP_INTERVAL_SECONDS must be a positive integer." >&2; exit 1 ;;
esac

umask 077
mkdir -p /backups

offsite_enabled=0
if [ -n "${OFFSITE_TARGET:-}" ]; then
  key_file="${OFFSITE_SSH_KEY_FILE:-/backups/offsite_key}"
  if [ -z "$OFFSITE_AGE_RECIPIENT" ]; then
    echo "Off-site backup disabled: OFFSITE_AGE_RECIPIENT is required to encrypt archives." >&2
  elif [ ! -r "$key_file" ]; then
    echo "Off-site backup disabled: SSH key $key_file is missing or unreadable." >&2
  elif apk add --no-cache age rsync openssh-client >/dev/null; then
    offsite_enabled=1
  else
    echo "Off-site backup disabled: could not install age, rsync, and openssh-client." >&2
  fi
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
  offsite_sources="$archive"
  find /backups -maxdepth 1 -type f -name "${PGDATABASE}-*.dump" \
    -mtime "+${BACKUP_RETENTION_DAYS}" -delete
  echo "Backup and restore check succeeded: $archive ($table_count public tables)"

  # Uploaded files (documents, report photos, product images) live outside the database.
  upload_backup_failed=0
  for dir in ${UPLOADS_DIRS:-}; do
    if [ -d "$dir" ]; then
      upload_archive="/backups/uploads-$(basename "$dir")-${timestamp}.tar.gz"
      if tar -czf "${upload_archive}.tmp" -C "$dir" . && mv "${upload_archive}.tmp" "$upload_archive"; then
        echo "Uploads backed up: $upload_archive"
        offsite_sources="$offsite_sources $upload_archive"
      else
        rm -f "${upload_archive}.tmp"
        echo "Uploads backup failed for $dir" >&2
        upload_backup_failed=1
      fi
    fi
  done
  find /backups -maxdepth 1 -type f -name "uploads-*.tar.gz" \
    -mtime "+${BACKUP_RETENTION_DAYS}" -delete

  # Encrypt with the public age key before sending archives off this server.
  if [ "$offsite_enabled" -eq 1 ]; then
    offsite_staging=/backups/offsite-staging
    mkdir -p "$offsite_staging"
    offsite_ok=1
    if [ "$upload_backup_failed" -eq 1 ]; then
      echo "Off-site copy skipped because at least one uploads archive failed." >&2
      offsite_ok=0
    else
      for source in $offsite_sources; do
        encrypted="$offsite_staging/$(basename "$source").age"
        encrypted_temporary="${encrypted}.tmp"
        rm -f "$encrypted_temporary"
        if age --encrypt --recipient "$OFFSITE_AGE_RECIPIENT" \
            --output "$encrypted_temporary" "$source" &&
            mv "$encrypted_temporary" "$encrypted"; then
          :
        else
          rm -f "$encrypted_temporary"
          echo "Off-site encryption failed for $source." >&2
          offsite_ok=0
          break
        fi
      done
    fi

    if [ "$offsite_ok" -eq 1 ]; then
      if rsync -a --partial \
          -e "ssh -p ${OFFSITE_SSH_PORT:-23} -i $key_file -o StrictHostKeyChecking=accept-new -o UserKnownHostsFile=/backups/.known_hosts" \
          --include='*.age' --exclude='*' \
          "$offsite_staging/" "${OFFSITE_TARGET%/}/"; then
        rm -f "$offsite_staging"/*.age
        echo "Encrypted off-site copy succeeded: $OFFSITE_TARGET"
      else
        echo "Encrypted off-site copy to $OFFSITE_TARGET failed; staged archives will be retried." >&2
      fi
    fi
  fi

  trap - EXIT HUP INT TERM
  sleep "$BACKUP_INTERVAL_SECONDS"
done
