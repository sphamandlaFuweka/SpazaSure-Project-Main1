-- ============================================================================
-- Fresh start for money records. DESTRUCTIVE AND PERMANENT.
--
-- Removes: supplier subscriptions (and their payment history), shop onboarding
-- payments, shop wallet top-ups/withdrawals and history, tax invoices and all
-- notifications. Every supplier goes back to the free Basic plan and shops are
-- marked as not having paid the onboarding fee.
--
-- Kept: subscription plans, suppliers, shops, products, users and settings.
-- Run on the server:
--   docker exec -i spazasure-prod-db psql -U spazasure_prod_user -d spazasure -v ON_ERROR_STOP=1 < clear_money_records.sql
-- ============================================================================
BEGIN;

SELECT 'before' AS stage,
       (SELECT count(*) FROM supplier_subscriptions) AS supplier_subscriptions,
       (SELECT count(*) FROM shop_onboarding_payments) AS onboarding_payments,
       (SELECT count(*) FROM shop_wallet_transactions) AS wallet_transactions,
       (SELECT count(*) FROM tax_invoices) AS tax_invoices,
       (SELECT count(*) FROM notifications) AS notifications;

DELETE FROM supplier_subscriptions;
DELETE FROM shop_onboarding_payments;
DELETE FROM shop_wallet_ledger_entries;
DELETE FROM shop_wallet_transactions;
DELETE FROM tax_invoices;
DELETE FROM notifications;

UPDATE suppliers
SET tier = 'basic',
    commission_rate = coalesce((SELECT commission_rate FROM subscription_plans WHERE tier = 'basic' LIMIT 1), commission_rate);

UPDATE spaza_shops SET onboarding_fee_paid = false, onboarding_fee_ref = NULL;

SELECT 'after' AS stage,
       (SELECT count(*) FROM supplier_subscriptions) AS supplier_subscriptions,
       (SELECT count(*) FROM shop_onboarding_payments) AS onboarding_payments,
       (SELECT count(*) FROM shop_wallet_transactions) AS wallet_transactions,
       (SELECT count(*) FROM tax_invoices) AS tax_invoices,
       (SELECT count(*) FROM notifications) AS notifications;
SELECT company_name, tier, commission_rate FROM suppliers;

COMMIT;
