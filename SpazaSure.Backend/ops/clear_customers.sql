-- ============================================================================
-- Removes EVERY customer-app account (users with the "customer" role) and all
-- of their data. DESTRUCTIVE AND PERMANENT: back up first.
--
-- Removes: customer accounts and profiles, subscriptions, scans, reward points,
-- vouchers, product reports they filed, shop reviews they wrote, chats with
-- shops, sign-in tokens, OTP codes, device tokens, deletion requests and their
-- notifications. Scanned QR codes stay used but no longer name the customer.
-- Shop star ratings are recalculated from the reviews that remain.
--
-- Suppliers, shops, admins, products and orders are not touched.
-- Run on the server (commands at the bottom of this file).
-- ============================================================================
BEGIN;

CREATE TEMP TABLE target_customers ON COMMIT DROP AS
SELECT u.id FROM users u JOIN roles r ON r.id = u.role_id WHERE r.name = 'customer';

SELECT 'before' AS stage,
       (SELECT count(*) FROM target_customers) AS customers,
       (SELECT count(*) FROM shop_reviews WHERE reviewer_user_id IN (SELECT id FROM target_customers)) AS reviews,
       (SELECT count(*) FROM reports WHERE reporter_user_id IN (SELECT id FROM target_customers)) AS reports,
       (SELECT count(*) FROM customer_scan_events WHERE customer_user_id IN (SELECT id FROM target_customers)) AS scans;

-- Tables that point at the customer without a cascading foreign key
DELETE FROM notifications             WHERE supplier_id IN (SELECT id::text FROM target_customers);
DELETE FROM otp_codes                 WHERE user_id IN (SELECT id FROM target_customers);
DELETE FROM device_tokens             WHERE user_id::text IN (SELECT id::text FROM target_customers);
DELETE FROM account_deletion_requests WHERE user_id::text IN (SELECT id::text FROM target_customers);
UPDATE auth_audit_logs     SET user_id = NULL            WHERE user_id::text IN (SELECT id::text FROM target_customers);
UPDATE product_unit_codes  SET consumed_by_user_id = NULL WHERE consumed_by_user_id::text IN (SELECT id::text FROM target_customers);

-- Profiles, subscriptions, scans, rewards, vouchers, reports, reviews, chats and
-- refresh tokens go with the user (ON DELETE CASCADE)
DELETE FROM users WHERE id IN (SELECT id FROM target_customers);

-- Shop ratings now reflect only the reviews that are left
UPDATE spaza_shops s
SET rating_avg   = coalesce((SELECT round(avg(r.rating)::numeric, 2) FROM shop_reviews r WHERE r.shop_id = s.id), 0),
    rating_count = (SELECT count(*) FROM shop_reviews r WHERE r.shop_id = s.id);

SELECT 'after' AS stage,
       (SELECT count(*) FROM users u JOIN roles r ON r.id = u.role_id WHERE r.name = 'customer') AS customers,
       (SELECT count(*) FROM customer_profiles) AS customer_profiles,
       (SELECT count(*) FROM shop_reviews) AS reviews_left;

COMMIT;

-- ----------------------------------------------------------------------------
-- How to run it on the production server (from /opt/spazasure):
--
-- 1. Copy this file up (run on YOUR PC, not the server):
--    scp "C:\SpazaSure-Project-Main1\SpazaSure-Project-main\SpazaSure.Backend\ops\clear_customers.sql" root@2.29.35.144:/opt/spazasure/
--
-- 2. On the server, back up first:
--    docker exec spazasure-prod-db pg_dump -U spazasure_prod_user -d spazasure -Fc > backups/before-clear-customers-$(date +%Y%m%d-%H%M).dump
--
-- 3. Remove the customers:
--    docker exec -i spazasure-prod-db psql -U spazasure_prod_user -d spazasure -v ON_ERROR_STOP=1 < clear_customers.sql
-- ----------------------------------------------------------------------------
