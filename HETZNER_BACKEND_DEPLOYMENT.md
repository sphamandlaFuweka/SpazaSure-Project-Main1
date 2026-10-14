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
- `api.spazasure.co.za` proxies API requests to the Gateway on local port 5181.
- `app.spazasure.co.za` remains the main app origin and serves its current web
  files from `/var/www/spazasure`.

Create DNS A records for `portal` and `api` pointing to the Hetzner server's
public IPv4 address. Keep `app` pointed at its current host. If you use AAAA
records, they must point to a working IPv6 endpoint as well.

The deployment uploads `SpazaSure.Backend/nginx-spazasure.conf` to
`/opt/spazasure`. It contains only the new API and portal hosts, so it can be
enabled without replacing the existing `app` site's TLS configuration. On the
server, install it and validate Nginx:

```sh
sudo install -m 0644 /opt/spazasure/nginx-spazasure.conf /etc/nginx/sites-available/spazasure-domains
sudo ln -sfn /etc/nginx/sites-available/spazasure-domains /etc/nginx/sites-enabled/spazasure-domains
sudo nginx -t
sudo systemctl reload nginx
```

After `api` and `portal` DNS resolve to this server, provision HTTPS certificates
for those two hostnames (Certbot's Nginx plugin will add their TLS listeners
and HTTP redirects). The existing `app` host's TLS setup is left unchanged:

```sh
sudo certbot --nginx --redirect -d api.spazasure.co.za -d portal.spazasure.co.za
```

The production Compose file binds port 5181 to loopback; Nginx is the public
entry point. The portal's `VITE_API_URL` is set to
`https://api.spazasure.co.za/api`; Flutter release builds use that URL by
default (development builds can override it with `--dart-define=API_URL=...`).

## Paystack

All online payments use Paystack in ZAR: shop onboarding fees, shop orders,
wallet top-ups, customer Plus, and supplier subscriptions. Add
`PROD_PAYSTACK_SECRET_KEY` to the GitHub production environment. For local
deployment, put it in `.env.prod`; for QA, use `QA_PAYSTACK_SECRET_KEY` with a
test-mode key.

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

Before applying the provider-reference migration in production, cancel any
existing auto-renewing subscriptions in the previous payment provider
dashboard. Their provider subscription IDs cannot be migrated into Paystack.

Pushes to `main` run the production backend and portal deployments after their
build checks pass. The workflows use `PROD_HOST`, `PROD_SSH_USER`, and
`PROD_SSH_KEY` GitHub secrets to deploy to `/opt/spazasure` and
`/var/www/spazasure-supplier`. This repository change cannot create DNS records
or issue certificates; complete those server/provider steps before expecting
the HTTPS domains to resolve.