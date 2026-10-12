# PostgreSQL backups

Production Compose runs `postgres-backup` alongside PostgreSQL. It creates a
custom-format dump, restores each dump into a temporary database to verify
recoverability, then keeps the verified archive under `./backups`. The default
retention is 14 days and the default interval is 24 hours. Override these with
`PROD_BACKUP_RETENTION_DAYS` and `PROD_BACKUP_INTERVAL_SECONDS` in `.env.prod`.

The backup directory is on the production host at `/opt/spazasure/backups`.
It is separate from the database's Docker volume, but it is still on the same
server and disk. Copy backups to a separate protected storage location for
disaster recovery; local backups alone do not protect against loss of the host.
Archives contain application data and must be kept private.

To confirm backups and restore checks on the production host:

```sh
cd /opt/spazasure
docker compose -f docker-compose.prod.yml --env-file .env.prod logs --tail=100 postgres-backup
ls -lh backups
```

To manually restore an archive, first provision an isolated PostgreSQL 16
database with enough disk space. Do not restore over the live database. Then
copy the archive into that database container and run:

```sh
pg_restore --exit-on-error --no-owner --dbname="$RESTORE_DATABASE_URL" backup.dump
```

Keep the restore database isolated and remove it only after validating the
restored data and application behavior.
