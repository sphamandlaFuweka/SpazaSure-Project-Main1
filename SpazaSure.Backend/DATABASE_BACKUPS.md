# PostgreSQL and upload backups

Production Compose runs `postgres-backup` alongside PostgreSQL. It creates a
custom-format database dump and restores it into a temporary database to verify
it. It also archives the local user, compliance, and delivery upload volumes.
The first backup runs when the container starts; the default interval is 24
hours and local retention is 14 days. The files are stored under
`/opt/spazasure/backups` with restrictive permissions.

Local backups are on the same server as production and are not disaster
recovery. The off-site option sends each completed database and uploads archive
to a Hetzner Storage Box over SSH, encrypted with `age` before it leaves the
server. Off-site archives are not automatically deleted, so monitor Storage
Box capacity and remove old archives only after checking that newer backups
are restorable.

## One-time Hetzner Storage Box setup

1. Create a Hetzner Storage Box, enable SSH support, and add an SSH public key
   for backups.
2. On the production host, create a dedicated key in the backup directory:

   ```sh
   cd /opt/spazasure
   ssh-keygen -t ed25519 -N "" -C spazasure-backup -f backups/offsite_key
   chmod 600 backups/offsite_key
   cat backups/offsite_key.pub
   ```

   Add the displayed public key to the Storage Box. Keep the private key only
   on the production host; do not commit it or reuse a personal SSH key.
3. On a trusted admin computer, create an `age` identity:

   ```sh
   age-keygen -o spazasure-backup-identity.txt
   age-keygen -y spazasure-backup-identity.txt
   ```

   The second command prints the public recipient (`age1...`). Store the
   identity file securely offline; it is required to decrypt backups and must
   not be placed on the production host or in GitHub.
4. In the GitHub **production environment** secrets, set:
   - `PROD_BACKUP_OFFSITE_TARGET` to the Storage Box destination, for example
     `u12345@u12345.your-storagebox.de:spazasure-backups`.
   - `PROD_BACKUP_AGE_RECIPIENT` to the public `age1...` recipient.

   The private age identity is not a GitHub secret because the backup job only
   needs the public encryption key. The deployment workflow rejects an
   off-site destination without an age recipient.
5. Ensure the remote directory exists, then deploy the backend. The backup
   container installs the required tools, runs the normal local backup, and
   starts sending encrypted copies. The destination format is
   `user@host:remote-directory`; the SSH port defaults to 23.

   For a Storage Box that permits SSH directory commands, create the remote
   directory with:

   ```sh
   ssh -p 23 -i backups/offsite_key u12345@u12345.your-storagebox.de \
     mkdir -p spazasure-backups
   ```

## Verify backups

On the production host:

```sh
cd /opt/spazasure
docker compose -f docker-compose.prod.yml --env-file .env.prod logs --tail=100 postgres-backup
ls -lh backups
```

Look for both “Backup and restore check succeeded” and “Encrypted off-site
copy succeeded” messages. Confirm the `.age` archives are present in the
Storage Box using its file browser or SFTP. A local backup alone does not
confirm that the remote copy or decryption key works; perform a restore test
before relying on the backup.

## Restore

Download the encrypted archive from the Storage Box to a trusted recovery
machine and decrypt it with the offline age identity. Restore into an isolated
database, never over the live database:

```sh
age --decrypt --identity spazasure-backup-identity.txt \
  --output spazasure.dump spazasure-YYYYMMDDTHHMMSSZ.dump.age
pg_restore --exit-on-error --no-owner --dbname="$RESTORE_DATABASE_URL" spazasure.dump
```

For uploads, decrypt the relevant archive and inspect it before extraction:

```sh
age --decrypt --identity spazasure-backup-identity.txt \
  --output uploads.tar.gz uploads-users-YYYYMMDDTHHMMSSZ.tar.gz.age
tar -tzf uploads.tar.gz
```

Restore uploads into an isolated directory or volume, then verify the
application can read them before changing production storage.

## Scope and limitations

- These backups cover PostgreSQL and the three local Docker upload volumes.
  If production switches to S3-compatible storage, configure bucket versioning
  or a separate object backup before switching; these volume archives do not
  include objects stored in S3.
- Backups are retained locally for 14 days by default. Off-site copies are
  intentionally not pruned by the server, so server loss or compromise cannot
  automatically delete the remote history.
- Keep the age identity separately from both the server and Storage Box. If it
  is lost, encrypted off-site archives cannot be recovered.
