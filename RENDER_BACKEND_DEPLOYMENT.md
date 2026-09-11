# Render Backend Deployment

The supplier portal is a Render Static Site defined in `render.yaml`. The .NET
backend cannot be deployed by Vercel or GitHub Actions because it is a set of
long-running services. Render needs one service for each backend process.

## Required Render services

Create these Docker services from the `SpazaSure.Backend` directory:

- `auth-service` (port 5001)
- `product-service` (port 5002)
- `order-service` (port 5003)
- `user-service` (port 5005)
- `analytics-service` (port 5004)
- `compliance-service` (port 5006)
- `notification-service` (port 5184)
- `gateway` (public web service, port 5181)

For each service, set `Docker Context` to `SpazaSure.Backend` and use its
existing Dockerfile under `src/Services` or `src/Gateway`.

## Required dependencies

- Render PostgreSQL. Run `SpazaSure.Backend/migrate.sql` once against it, then
  run `seed_data.sql` only when demo data is wanted.
- A hosted RabbitMQ instance, such as CloudAMQP. Render does not provide
  RabbitMQ as a managed service. Set `RabbitMQ:Host`, `RabbitMQ:Username`, and
  `RabbitMQ:Password` on the services that publish or consume events.
- S3-compatible object storage, such as Cloudflare R2 or AWS S3, because
  Render's local filesystem is not persistent across deploys. Configure the
  `Storage:S3:*` values on `user-service` and `compliance-service`.

## Shared environment variables

Set these on every service that accesses the database:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=<Render PostgreSQL connection string>
Jwt__Secret=<random secret of at least 32 characters>
Cors__AllowedOrigins__0=<deployed Render portal URL>
```

The gateway must expose port `5181` and its private destinations must resolve
to the Render service names (`auth-service`, `product-service`, and so on).
Set the portal's `VITE_API_URL` to the public gateway URL followed by `/api`.

The old SSH deployment workflow has been removed. Render deploys directly from
the GitHub repository whenever the configured branch changes.