# Hetzner Backend Deployment

The backend and supplier portal run on the Hetzner server through Docker Compose
and Nginx. GitHub Actions builds the backend images and deploys them over SSH.

## Required backend services

Run these services from the `SpazaSure.Backend` directory:

- `auth-service` (port 5001)
- `product-service` (port 5002)
- `order-service` (port 5003)
- `user-service` (port 5005)
- `analytics-service` (port 5004)
- `compliance-service` (port 5006)
- `notification-service` (port 5184)
- `gateway` (public web service, port 5181)

The production compose file already references the service images and exposes
only the Gateway publicly.

## Required dependencies

- PostgreSQL on Hetzner or a managed PostgreSQL provider. Run
  `SpazaSure.Backend/migrate.sql` once, then run `seed_data.sql` only when demo
  data is wanted. Before deploying the app OTP changes, rerun the idempotent
  migration so `otp_codes.failed_attempts` exists; the production `db-init`
  service runs this migration before the application services start.
- A hosted RabbitMQ instance, such as CloudAMQP. Set `RabbitMQ:Host`,
  `RabbitMQ:Username`, and `RabbitMQ:Password` on the services that publish or
  consume events.
- Optional S3-compatible object storage, such as Cloudflare R2 or AWS S3.
  Configure the `Storage:S3:*` values on `user-service` and `compliance-service`
  when external object storage is preferred over Hetzner Docker volumes.

## Shared environment variables

Set these on every service that accesses the database:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=<Hetzner PostgreSQL connection string>
Jwt__Secret=<random secret of at least 32 characters>
Cors__AllowedOrigins__0=https://app.spazasure.co.za
Cors__AllowedOrigins__1=https://portal.spazasure.co.za
Cors__AllowedOrigins__2=https://admin.spazasure.co.za
```

Mobile app accounts for shop owners and customers use a phone number and
password. Passwords are stored as BCrypt hashes; five consecutive failed
password attempts lock the account for 15 minutes. There is no email or SMS
dependency for app sign-in. Forgotten mobile passwords must be handled by
support after manual account verification; do not reset an account based only
on an unverified phone-number claim. Supplier/admin sign-ins to the web portal
remain on the existing email/password flow.
Shop accounts created under the earlier OTP-only flow may have no password
hash; support must set a password for those accounts before their next sign-in.

## Domains and Nginx

Use these hostnames:

- `portal.spazasure.co.za` serves the Supplier Portal from
  `/var/www/spazasure-supplier`.
- `admin.spazasure.co.za` serves the Admin Console from
  `/var/www/spazasure-admin`. It is a separate build of the same codebase, so the
  supplier portal contains no admin pages or code, and each site only accepts
  its own kind of account at sign-in.
- `api.spazasure.co.za` proxies API requests to the Gateway on local port 5181.
- `app.spazasure.co.za` remains the main app origin and serves its current web
  files from `/var/www/spazasure`.

Create DNS A records for `portal`, `admin` and `api` pointing to the Hetzner server's
public IPv4 address. Keep `app` pointed at its current host. If you use AAAA
records, they must point to a working IPv6 endpoint as well.

The deployment uploads `SpazaSure.Backend/nginx-spazasure.conf` to
`/opt/spazasure`. It contains the `api`, `portal` and `admin` hosts (the `app`
host is configured separately), so it can be enabled without replacing the
existing `app` site's TLS configuration. On a new server, install it and validate
Nginx:

```sh
sudo install -m 0644 /opt/spazasure/nginx-spazasure.conf /etc/nginx/sites-available/spazasure-domains
sudo ln -sfn /etc/nginx/sites-available/spazasure-domains /etc/nginx/sites-enabled/spazasure-domains
sudo nginx -t
sudo systemctl reload nginx
```

After `api`, `portal` and `admin` DNS resolve to this server, provision HTTPS certificates
for those hostnames (Certbot's Nginx plugin will add their TLS listeners
and HTTP redirects). The existing `app` host's TLS setup is left unchanged:

```sh
sudo certbot --nginx --redirect -d api.spazasure.co.za -d portal.spazasure.co.za -d admin.spazasure.co.za
```

The production Compose file binds port 5181 to loopback; Nginx is the public
entry point. The portal's `VITE_API_URL` is set to
`https://api.spazasure.co.za/api`; Flutter release builds use that URL by
default (development builds can override it with `--dart-define=API_URL=...`).

## Paystack

All online payments use Paystack in ZAR: shop onboarding fees, shop orders,
wallet top-ups, customer Plus, and supplier subscriptions. When Paystack is
approved and ready for live payments, add `PROD_PAYSTACK_SECRET_KEY` to the
GitHub production environment. Until then, it may remain unset; checkout
endpoints stay disabled while other backend services can still be deployed.
For local deployment, put the key in `.env.prod`; for QA, use
`QA_PAYSTACK_SECRET_KEY` with a test-mode key.

The backend can deploy without `PROD_PAYSTACK_SECRET_KEY`; online checkout
remains unavailable and Paystack webhooks are rejected until a key is configured.
Set the production environment secret and redeploy to enable payments. An unset
or empty key must not be replaced with a placeholder key.

Configure the Paystack webhook at
`https://api.spazasure.co.za/api/supplier/payment/paystack/webhook`. The
application verifies Paystack's HMAC signature and independently verifies each
transaction before marking it paid. Enable `charge.success`, `subscription.create`,
`subscription.disable`, and `invoice.payment_failed` events. Set checkout return
URLs with the `PROD_PAYSTACK_*_CALLBACK_URL` variables; QA equivalents use the
`QA_PAYSTACK_*_CALLBACK_URL` names.

Recurring billing uses Paystack plans, which must be created in the Paystack
Dashboard using the correct ZAR amount and interval. Add each returned plan code
to the matching production GitHub secret:

- `PROD_PAYSTACK_PLAN_CUSTOMER_PLUS_MONTHLY` — R19.99 per month.
- `PROD_PAYSTACK_PLAN_SUPPLIER_BRONZE_MONTHLY` / `_ANNUAL`.
- `PROD_PAYSTACK_PLAN_SUPPLIER_SILVER_MONTHLY` / `_ANNUAL`.
- `PROD_PAYSTACK_PLAN_SUPPLIER_GOLD_MONTHLY` / `_ANNUAL`.

The supplier plan amounts are the active monthly/annual prices in the
`subscription_plans` table. Configure matching QA plan-code variables for
test-mode plans. The backend checks every plan's currency, amount, and interval
before creating checkout. A missing plan code disables only that subscription
checkout and returns a configuration error. Cancellation disables recurring
billing immediately and ends the paid plan immediately; the supplier is
downgraded to Basic.

Shop wallet top-ups are limited to R50–R2,000 per transaction. Order payments
are confirmed only by a signed webhook and server-side transaction verification.

**Shop Paystack orders use a direct multi-split.** Each supplier receives their
product subtotal in a Paystack subaccount; SpazaSure receives the delivery fee
and platform commission in its main Paystack account. A successful checkout
allocates supplier shares immediately; the supplier's bank settlement timing
still follows Paystack's schedule. Paystack Subaccounts and Multi-split Payments
must be enabled for the account. Every supplier taking Paystack orders must have
valid South African bank details in their profile; checkout fails rather than
falling back to a pooled payment if Paystack cannot create the split. Supplier
order views show product value only, not delivery fees or SpazaSure's share.
If a paid split order is later cancelled, an admin must refund through Paystack
and arrange recovery of the supplier share separately; this recovery is not
automated or tracked by SpazaSure.

This split applies to Paystack card checkouts. Cash-on-delivery payments are
collected by SpazaSure, and wallet-funded orders continue through the delayed
supplier-payout flow below.

Before applying the provider-reference migration in production, cancel any
existing auto-renewing subscriptions in the previous payment provider
dashboard. Their provider subscription IDs cannot be migrated into Paystack.

## Deliveries and supplier payouts

**SpazaSure delivers.** A supplier accepts an order, packs it and marks it **Ready
for collection**; admins get an alert. SpazaSure staff then work from the admin
console's **Deliveries** page: **Collected · out for delivery** when they pick it
up, and **Delivered** when the shop has it (for cash on delivery, after collecting
the cash; the shop can also confirm with the delivery code). Suppliers can only
mark **pickup** orders delivered, when the shop collects from them.

**Suppliers are paid for the goods** (the order subtotal). SpazaSure keeps the
delivery fee and the commission and service fee the shop paid. Paystack card
orders allocate that product share when payment succeeds; wallet and
cash-on-delivery orders become available the configured number of days after
delivery (Settings → payout delay).

**Automatic payouts (Paystack Transfers).** With `PROD_PAYSTACK_AUTOMATIC_PAYOUTS`
set to `true`, the order service pays every supplier whose money is available,
every 30 minutes, from the Paystack balance into the bank account on their
profile (they choose the bank from Paystack's list, which fills in the universal
branch code). The `transfer.success`, `transfer.failed` and `transfer.reversed`
webhook events mark payouts paid or failed. A failed payout is not retried
automatically (each attempt costs R3) until the supplier updates their bank
details or an admin clicks **Retry** on the Payouts page. Before turning it on:

1. Transfers must be enabled on the Paystack account (registered businesses only).
2. Turn off OTP for transfers (Paystack dashboard → Settings → Preferences);
   otherwise every payout waits for a PIN.
3. Keep enough money in the Paystack balance: with automatic settlement to your
   bank, the balance may be empty when payouts run. Admins get an alert when the
   balance is too low, and payouts continue once it's topped up.
4. Make sure the Live Webhook URL is set (see Paystack above).

With it off (the default), admins pay suppliers by EFT and record it with
**Mark as paid**.

## Didit identity checks

> **Suppliers and admin checks need no link.** A supplier's "Verify my business"
> and an admin's Verify button run on the server through Didit's KYB registry
> search (CIPC) and AML screening APIs, and the result appears by itself within a
> minute or two. Make sure **KYB registry** and **AML screening** are enabled on the
> Didit account. For an admin check of a shop owner, the admin confirms they have
> seen the owner's original ID in person; the owner is then screened against
> sanctions lists. Only a shop owner's own "Verify my identity" in the app uses the
> Didit KYC workflow (ID photo and selfie). The supplier KYB workflow and
> `PROD_DIDIT_SUPPLIER_WORKFLOW_ID` are no longer used.

Shop owners verify their identity (ID document, selfie, Home Affairs and
sanctions) from the app's Compliance screen, and suppliers verify their business
(CIPC registration, directors and sanctions) from Profile → Documents. Both run on
Didit's hosted pages; the compliance service starts each session and receives the
result by webhook. A shop needs an approved owner check for its SpazaSure Verified
sticker. This replaces the manually reviewed owner ID document. Admins see each
result, with Didit's warnings, on the Document Verification page.

In the Didit console:

1. Create a **KYC workflow** for shop owners: ID verification, liveness, face
   match, South Africa National ID (DHA) database validation and AML screening.
2. Create a **KYB workflow** for suppliers: South Africa company registry and
   company directors (CIPC) and AML screening.
3. Add a webhook destination for `status.updated` events with the URL
   `https://api.spazasure.co.za/api/compliance/identity/didit/webhook`.

Then add these GitHub production secrets and redeploy the backend:

| Secret | Value |
|---|---|
| `PROD_DIDIT_API_KEY` | The application's API key |
| `PROD_DIDIT_WEBHOOK_SECRET` | The webhook destination's secret |
| `PROD_DIDIT_SHOP_WORKFLOW_ID` | The KYC workflow ID |
| `PROD_DIDIT_SUPPLIER_WORKFLOW_ID` | The KYB workflow ID |

Until they are set, the "Verify" buttons report that checks are unavailable and
the webhook returns 503. Use a sandbox API key first; sandbox and live use
different keys and workflows. While testing with a sandbox key, also set
`PROD_DIDIT_SANDBOX` to `true`: sandbox returns made-up ID and company data, so
the check that the document or company matches the profile is skipped. Remove it
(or set `false`) when switching to the live key.

When Didit runs out of credit, users see that checks are temporarily unavailable
and admins get an alert (at most hourly). Top up at business.didit.me.

## Privacy (POPIA)

- The privacy notice is published at `https://portal.spazasure.co.za/privacy`.
  Fill in the company and Information Officer details at the top of
  `supplier-portal/src/pages/public/PrivacyNoticePage.tsx`, and have the wording
  reviewed before launch. When the wording changes, update the version there and
  in `SpazaSure.Shared/Helpers/PrivacyNotice.cs`; everyone is then asked to accept
  the new version at their next sign-in.
- Registration requires accepting the notice. Consents are recorded in
  `auth_audit_logs` as `consent_{kind}:{version}`.
- Users can download their data and ask for their account to be deleted. Master
  admins approve deletions on the admin console's Deletion Requests page (respond
  within 30 days).
- Compliance documents and report photos are only served through links the API
  signs for 30 minutes. Keep uploads on the server's local storage: private files
  in S3 would need pre-signed links, which are not implemented yet.

## Monitoring

`https://api.spazasure.co.za/health` returns 200 when every service is running and
can reach the database, and 503 (listing what is down) otherwise. Point an uptime
monitor such as UptimeRobot at it, checking every 5 minutes and alerting by email
or SMS.

## Backups

The `postgres-backup` container makes a verified database backup and an archive of
uploaded documents and photos every day, kept for 14 days in
`/opt/spazasure/backups`. To also copy them off the server (recommended), for
example to a Hetzner Storage Box:

1. Order a Storage Box and enable SSH access.
2. On the server, create a key: `ssh-keygen -t ed25519 -N "" -f /opt/spazasure/backups/offsite_key`
3. Add `offsite_key.pub` to the Storage Box's authorised keys (see Hetzner's Storage Box SSH docs).
4. Set the GitHub secret `PROD_BACKUP_OFFSITE_TARGET` to e.g.
   `u12345@u12345.your-storagebox.de:spazasure` and redeploy.

Off-site copies are never deleted by the server, so clear old ones on the Storage
Box yourself (or use its snapshots). Check `docker logs spazasure-prod-db-backup`
for "Off-site copy succeeded".

## Security headers

The API gateway sends HSTS, `X-Content-Type-Options`, `Referrer-Policy` and a
`frame-ancestors` policy on every response. The repository's Nginx config adds the
same headers for `portal` and `admin`. On a server where Certbot has already edited
the live site files, add these lines inside each `server { ... }` block for
`portal` and `admin` (in `/etc/nginx/sites-available/`), then run
`sudo nginx -t && sudo systemctl reload nginx`:

```nginx
add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
add_header X-Content-Type-Options "nosniff" always;
add_header X-Frame-Options "SAMEORIGIN" always;
add_header Referrer-Policy "strict-origin-when-cross-origin" always;
```

Pushes to `main` run the production backend and portal deployments after their
build checks pass. The workflows use `PROD_HOST`, `PROD_SSH_USER`, and
`PROD_SSH_KEY` GitHub secrets to deploy to `/opt/spazasure` and
`/var/www/spazasure-supplier` (supplier portal) and `/var/www/spazasure-admin`
(admin console). This repository change cannot create DNS records
or issue certificates; complete those server/provider steps before expecting
the HTTPS domains to resolve.