-- ============================================================================
-- Clears every order, and everything that belongs to an order, so trading
-- starts from a clean slate. DESTRUCTIVE AND PERMANENT: back up first.
--
-- Removes: orders and their items, tax invoices, delivery ratings and
-- disputes, wallet payments for orders, group buys (with participants), and
-- the notifications that point at any of these.
-- Keeps: products (with their current stock levels), suppliers, shops,
-- customers, admins, wallet top-ups and withdrawals, settings.
--
-- Run on the server (see the commands at the bottom of this file).
-- ============================================================================
BEGIN;

SELECT 'before' AS stage,
       (SELECT count(*) FROM orders) AS orders,
       (SELECT count(*) FROM group_buys) AS group_buys,
       (SELECT count(*) FROM products) AS products;

-- Notifications that would otherwise open an order or group buy that no longer exists
DELETE FROM notifications
WHERE reference_id IN (SELECT id::text FROM orders UNION SELECT id::text FROM group_buys);

-- Wallet payments for orders. Top-ups and withdrawals stay, so each shop's balance
-- becomes what it deposited minus what it withdrew.
DELETE FROM shop_wallet_ledger_entries WHERE source_id IN (SELECT id FROM orders);

-- Authenticity codes that were allocated to an order item go back to unallocated
UPDATE product_qr_codes SET order_item_id = NULL WHERE order_item_id IS NOT NULL;

DELETE FROM tax_invoices;
-- order_items, delivery_disputes and delivery_ratings go with their orders (ON DELETE CASCADE)
DELETE FROM orders;
-- group_buy_participant_items go with their participants; group_buy_products with their group buy
DELETE FROM group_buy_participants;
DELETE FROM group_buys;

SELECT 'after' AS stage,
       (SELECT count(*) FROM orders) AS orders,
       (SELECT count(*) FROM group_buys) AS group_buys,
       (SELECT count(*) FROM products) AS products;

COMMIT;

-- ----------------------------------------------------------------------------
-- How to run it on the production server (from the SpazaSure.Backend folder):
--
-- 1. Back up first:
--    docker exec spazasure-prod-db pg_dump -U spazasure_prod_user -d spazasure -Fc \
--      > backups/before-clear-orders-$(date +%Y%m%d-%H%M).dump
--
-- 2. Clear the orders:
--    docker exec -i spazasure-prod-db psql -U spazasure_prod_user -d spazasure \
--      -v ON_ERROR_STOP=1 < ops/clear_orders.sql
--
-- To undo, restore the backup:
--    docker exec -i spazasure-prod-db pg_restore -U spazasure_prod_user -d spazasure \
--      --clean --if-exists < backups/before-clear-orders-<timestamp>.dump
-- ----------------------------------------------------------------------------
