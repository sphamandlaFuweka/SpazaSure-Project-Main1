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
  data is wanted.
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
```

The gateway must expose port `5181` behind Nginx. Set the portal's
`VITE_API_URL` and Flutter's `API_URL` to `https://api.spazasure.co.za/api`.

The production workflows use `PROD_HOST`, `PROD_SSH_USER`, and `PROD_SSH_KEY`
GitHub secrets to deploy the backend and portal to `/opt/spazasure` and
`/var/www/spazasure-supplier`.