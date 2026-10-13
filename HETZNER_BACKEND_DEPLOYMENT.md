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

## Stripe

Add these as GitHub Actions secrets in the **production** environment:

- `PROD_STRIPE_SECRET_KEY` (use a Stripe test key for testing; a live key
  processes real payments)
- `PROD_STRIPE_WEBHOOK_SECRET` (signing secret for the webhook below)

In Stripe, register the webhook URL
`https://api.spazasure.co.za/api/supplier/payment/stripe/webhook` and enable
`checkout.session.completed`, `checkout.session.async_payment_succeeded`,
`checkout.session.expired`, `checkout.session.async_payment_failed`,
`customer.subscription.updated`, and `customer.subscription.deleted`. These
events confirm shop order payments,
supplier plan renewals, and customer subscription lifecycle changes. Supplier
monthly and annual plans now auto-renew through Stripe; cancellation takes
effect at the end of the paid period. Customer Plus checkout is a recurring
R19.99 ZAR monthly Stripe subscription. The Family plan is listed as coming
soon and cannot be purchased yet. Shop owners can top up their wallet through
Stripe Checkout for R50–R2,000 per transaction. Top-ups are credited only after
the signed Stripe webhook confirms a paid session. Wallet checkout returns to
the API hostname; supplier subscriptions and customer payments use the portal
and app URLs above.

Pushes to `main` run the production backend and portal deployments after their
build checks pass. The workflows use `PROD_HOST`, `PROD_SSH_USER`, and
`PROD_SSH_KEY` GitHub secrets to deploy to `/opt/spazasure` and
`/var/www/spazasure-supplier`. This repository change cannot create DNS records
or issue certificates; complete those server/provider steps before expecting
the HTTPS domains to resolve.