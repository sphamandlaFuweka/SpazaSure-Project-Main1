# Africa's Talking phone verification

Shop-owner and customer registration can require a one-time SMS code. The feature is
disabled by default, so existing registration and login behavior is unchanged until
the provider account is funded and configured.

## Configuration

Set these AuthService environment variables only after the Africa's Talking account
is ready:

| Variable | Purpose |
| --- | --- |
| `PhoneVerification__Enabled` | Set to `true` to require phone verification for new shop-owner and customer registrations |
| `PhoneVerification__HashKey` | A private random key of at least 32 characters used to hash stored OTPs |
| `AfricaTalking__Username` | Africa's Talking application username |
| `AfricaTalking__ApiKey` | Africa's Talking API key |
| `AfricaTalking__SenderId` | Optional approved sender ID; omit if using the account's default route |

Keep the API key and hash key in the deployment secret store, never in source control.
AuthService refuses to start if verification is enabled without the required keys.

The app reads the enabled state from AuthService, then shows code entry on both
registration forms. Codes expire after five minutes, allow five failed attempts, and
are limited to five requests per phone per day with a 60-second resend cooldown.
The API Gateway also rate-limits send-code requests by client IP.

## Before enabling in production

1. Fund the Africa's Talking account and confirm the South African route, sender-ID
   approval requirements, and current per-SMS price in the account dashboard.
2. Set the secrets above in the AuthService deployment environment, initially leaving
   `PhoneVerification__Enabled` as `false`.
3. Verify delivery to South African mobile networks using a controlled test account.
4. Enable `PhoneVerification__Enabled` only after a successful test.

No database migration is needed; OTP records use the existing `otp_codes` table.
