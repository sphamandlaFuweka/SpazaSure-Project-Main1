# Paystack fee pass-through

SpazaSure's eligible Paystack checkouts add the confirmed South African card tariff to the amount the platform or supplier is due:

- Percentage fee: 3.335% of the Paystack charge (2.9% plus 15% VAT)
- Fixed fee: R1.15 (R1.00 plus 15% VAT)
- Gross charge: the smallest cent amount for which `charge - round(charge * 0.03335 + R1.15, 2) >= base amount`

The fee is included in the Paystack total, not shown as a separate fee line. Wallet and cash-on-delivery order totals, wallet credits, supplier split shares, and the refundable saved-card check remain at their base amounts. The database continues storing base order, wallet, onboarding, and subscription amounts; webhook validation accepts both base and fee-inclusive amounts so pending/legacy checkouts can still complete.

## Recurring plans

Paystack recurring plans must be created at the fee-inclusive amounts or checkout initialization will reject a plan whose configured amount does not match the backend. Create replacement plans for each currently enabled recurring tier using the backend fee calculator's gross amount for the monthly or annual base plan price. Keep existing plan codes for current subscribers so their existing renewals are not changed; use the new plan codes for new subscriptions.

Set the new plan codes in QA/production environment variables (`*_PAYSTACK_PLAN_CUSTOMER_PLUS_MONTHLY`, `*_PAYSTACK_PLAN_CUSTOMER_BULK_MONTHLY`, and the supplier monthly/annual variables) before enabling recurring checkouts. The keys are mapped in `docker-compose.qa.yml` and `docker-compose.prod.yml`. No Paystack account or plan is modified by this code change.

## Reference totals

| Base amount | Paystack charge | Approx. fee |
|---:|---:|---:|
| R20.00 | R21.88 | R1.88 |
| R97.29 | R101.84 | R4.55 |
| R100.00 | R104.64 | R4.64 |
