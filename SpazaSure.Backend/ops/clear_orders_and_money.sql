-- ============================================================================
-- Fresh start for trading: every order and every money record. DESTRUCTIVE
-- AND PERMANENT.
--
-- Removes: orders and their items, tax invoices, delivery ratings and
-- disputes, group buys (with participants), supplier subscriptions (and their
-- payment history), shop onboarding payments, shop wallet top-ups, payments,
-- withdrawals and history, and all notifications. That empties the admin
-- Finance page: SpazaSure budget, supplier payouts and driver pay all come
-- from orders.
-- Every supplier goes back to the free Basic plan and shops are marked as not
-- having paid the onboarding fee.
--
-- Kept: products (with their current stock levels), suppliers, shops,
-- drivers, customers, admins, documents, shop stock books and settings.
--
-- Run on the server (from the SpazaSure.Backend folder):
--   docker exec -i spazasure-prod-db psql -U spazasure_prod_user -d spazasure \
--     -v ON_ERROR_STOP=1 < ops/clear_orders_and_money.sql
-- ============================================================================
BEGIN;

SELECT 'before' AS stage,
       (SELECT count(*) FROM orders) AS orders,
       (SELECT count(*) FROM group_buys) AS group_buys,
       (SELECT count(*) FROM tax_invoices) AS tax_invoices,
       (SELECT count(*) FROM supplier_subscriptions) AS supplier_subscriptions,
       (SELECT count(*) FROM shop_onboarding_payments) AS onboarding_payments,
       (SELECT count(*) FROM shop_wallet_transactions) AS wallet_transactions,
       (SELECT count(*) FROM shop_wallet_ledger_entries) AS wallet_ledger,
       (SELECT count(*) FROM notifications) AS notifications;

-- Authenticity codes that were allocated to an order item go back to unallocated
UPDATE product_qr_codes SET order_item_id = NULL WHERE order_item_id IS NOT NULL;
-- Shops keep their stock books; received stock just no longer links to an order
UPDATE shop_stock_movements SET source_order_id = NULL WHERE source_order_id IS NOT NULL;

DELETE FROM tax_invoices;
-- order_items, delivery_disputes and delivery_ratings go with their orders (ON DELETE CASCADE)
DELETE FROM orders;
-- group_buy_participant_items go with their participants; group_buy_products with their group buy
DELETE FROM group_buy_participants;
DELETE FROM group_buys;

DELETE FROM supplier_subscriptions;
DELETE FROM shop_onboarding_payments;
DELETE FROM shop_wallet_ledger_entries;
DELETE FROM shop_wallet_transactions;
DELETE FROM notifications;

UPDATE suppliers
SET tier = 'basic',
    commission_rate = coalesce((SELECT commission_rate FROM subscription_plans WHERE tier = 'basic' LIMIT 1), commission_rate);

UPDATE spaza_shops SET onboarding_fee_paid = false, onboarding_fee_ref = NULL;

SELECT 'after' AS stage,
       (SELECT count(*) FROM orders) AS orders,
       (SELECT count(*) FROM group_buys) AS group_buys,
       (SELECT count(*) FROM tax_invoices) AS tax_invoices,
       (SELECT count(*) FROM supplier_subscriptions) AS supplier_subscriptions,
       (SELECT count(*) FROM shop_onboarding_payments) AS onboarding_payments,
       (SELECT count(*) FROM shop_wallet_transactions) AS wallet_transactions,
       (SELECT count(*) FROM shop_wallet_ledger_entries) AS wallet_ledger,
       (SELECT count(*) FROM notifications) AS notifications,
       (SELECT count(*) FROM products) AS products_kept;

COMMIT;
