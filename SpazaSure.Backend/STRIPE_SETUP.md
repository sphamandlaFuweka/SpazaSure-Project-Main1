# Stripe Shop Onboarding Setup

Stripe Checkout is currently used for the shop onboarding fee and supplier subscriptions. The secret key and webhook signing secret must stay in backend environment variables; never put them in Flutter code or commit `.env.qa` / `.env.prod`.

## QA

1. Copy `.env.qa.example` to `.env.qa` and set `QA_STRIPE_SECRET_KEY` to a Stripe test secret key.
2. Start the local QA stack with Docker Compose.
3. In a second terminal, forward Stripe test events to the Gateway:

   ```powershell
   stripe listen --forward-to http://localhost:5181/api/supplier/payment/stripe/webhook
   ```

4. Copy the `whsec_...` value printed by Stripe CLI into `QA_STRIPE_WEBHOOK_SECRET`, then recreate the user service:

   ```powershell
   docker compose --env-file .env.qa -f docker-compose.qa.yml up -d --build user-service
   ```

5. Set `QA_STRIPE_SUCCESS_URL` and `QA_STRIPE_CANCEL_URL` to the matching onboarding `return` and `cancel` URLs. The example values are for a local backend.

Use Stripe's test card `4242 4242 4242 4242`, any future expiry date, and any CVC to exercise Checkout in test mode.

## Production

Set `PROD_STRIPE_SECRET_KEY`, `PROD_STRIPE_WEBHOOK_SECRET`, `PROD_STRIPE_SUCCESS_URL`, and `PROD_STRIPE_CANCEL_URL` in the server's untracked `.env.prod`. Use the live secret key and the public HTTPS API return/cancel URLs. In the Stripe Dashboard, register `https://<api-host>/api/supplier/payment/stripe/webhook` and subscribe it to `checkout.session.completed`; put that endpoint's signing secret in `PROD_STRIPE_WEBHOOK_SECRET`.

Redeploy/recreate `user-service` after changing the environment values. The checkout endpoint returns HTTP 503 until all four Stripe settings are present, preventing payments that cannot be confirmed by webhook.