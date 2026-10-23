-- ============================================================================
-- Resets ONE shop's SpazaSure wallet to R0 by removing its top-ups,
-- withdrawals and wallet history. DESTRUCTIVE AND PERMANENT: back up first.
--
-- The shop is chosen by its exact name (not case sensitive). Change the name
-- in the line marked SHOP NAME below to reset a different shop. The script
-- stops without changing anything unless exactly one shop has that name.
-- Other shops, orders and products are not touched.
--
-- Run on the server (commands at the bottom of this file).
-- ============================================================================
BEGIN;

CREATE TEMP TABLE target_shop ON COMMIT DROP AS
SELECT id, shop_name, shop_code FROM spaza_shops
WHERE lower(trim(shop_name)) = lower('zozo');   -- SHOP NAME

DO $$
DECLARE found integer;
BEGIN
    SELECT count(*) INTO found FROM target_shop;
    IF found <> 1 THEN
        RAISE EXCEPTION 'Expected exactly 1 shop with that name, found %. Nothing was changed.', found;
    END IF;
END $$;

SELECT 'before' AS stage, t.shop_name, t.shop_code,
       (SELECT coalesce(sum(amount), 0) FROM shop_wallet_ledger_entries e WHERE e.shop_id = t.id) AS wallet_balance,
       (SELECT count(*) FROM shop_wallet_transactions w WHERE w.shop_id = t.id) AS top_ups_and_withdrawals
FROM target_shop t;

-- Notifications about those top-ups and withdrawals (e.g. "Wallet topped up")
DELETE FROM notifications
WHERE reference_id IN (SELECT w.id::text FROM shop_wallet_transactions w JOIN target_shop t ON t.id = w.shop_id);

DELETE FROM shop_wallet_ledger_entries WHERE shop_id IN (SELECT id FROM target_shop);
DELETE FROM shop_wallet_transactions WHERE shop_id IN (SELECT id FROM target_shop);

SELECT 'after' AS stage, t.shop_name,
       (SELECT coalesce(sum(amount), 0) FROM shop_wallet_ledger_entries e WHERE e.shop_id = t.id) AS wallet_balance,
       (SELECT count(*) FROM shop_wallet_transactions w WHERE w.shop_id = t.id) AS top_ups_and_withdrawals
FROM target_shop t;

COMMIT;

-- ----------------------------------------------------------------------------
-- How to run it on the production server (from /opt/spazasure):
--
-- 1. Copy this file up (run on YOUR PC, not the server):
--    scp "C:\SpazaSure-Project-Main1\SpazaSure-Project-main\SpazaSure.Backend\ops\clear_shop_wallet.sql" root@2.29.35.144:/opt/spazasure/
--
-- 2. On the server, back up first:
--    docker exec spazasure-prod-db pg_dump -U spazasure_prod_user -d spazasure -Fc > backups/before-clear-wallet-$(date +%Y%m%d-%H%M).dump
--
-- 3. Reset the wallet:
--    docker exec -i spazasure-prod-db psql -U spazasure_prod_user -d spazasure -v ON_ERROR_STOP=1 < clear_shop_wallet.sql
-- ----------------------------------------------------------------------------
