-- SpazaSure local/QA seed data: the minimum to log in and try every app.
-- One admin, the SpazaSure (Pty) Ltd supplier with its product catalogue, one spaza shop
-- and one customer, plus the supplier subscription plans. No orders, group buys or other
-- activity: those come from using the apps.
-- Run AFTER migrate.sql (which creates the schema plus baseline roles/categories).
-- Safe to re-run: every statement is idempotent (WHERE NOT EXISTS / ON CONFLICT).
--
-- Login credentials (password is the same for all four, for convenience):
--   Admin:      admin@spazasure.co.za     / Password123!
--   Supplier:   supplier@spazasure.co.za  / Password123!
--   Shop owner: sipho@cornershop.co.za    / Password123!
--   Customer:   zanele@example.co.za      / Password123!
--
-- The password hash below is a bcrypt hash of "Password123!" (cost 10),
-- compatible with BCrypt.Net-Next as used in AuthenticationService.cs.

BEGIN;

-- =========================================================================
-- 0. Remove the sample orders and group buy older versions of this file
--    created. Only those exact demo rows (fixed IDs) are touched.
-- =========================================================================

DELETE FROM tax_invoices WHERE order_id IN (
    '00000000-0000-0000-0006-000000000001', '00000000-0000-0000-0006-000000000002', '00000000-0000-0000-0006-000000000003');
DELETE FROM order_items WHERE order_id IN (
    '00000000-0000-0000-0006-000000000001', '00000000-0000-0000-0006-000000000002', '00000000-0000-0000-0006-000000000003');
DELETE FROM orders WHERE id IN (
    '00000000-0000-0000-0006-000000000001', '00000000-0000-0000-0006-000000000002', '00000000-0000-0000-0006-000000000003');
DELETE FROM group_buy_participants WHERE group_buy_id = '00000000-0000-0000-0007-000000000001';
DELETE FROM group_buys WHERE id = '00000000-0000-0000-0007-000000000001';

-- =========================================================================
-- 1. Demo users (one per role). Previously this file assumed a supplier
--    with a fixed ID already existed — on a freshly migrated database it
--    never did, so every statement below it silently no-op'd or failed on
--    the FK constraint. This block creates the users those records depend on.
-- =========================================================================

INSERT INTO users (id, email, phone, password_hash, role_id, status)
SELECT '00000000-0000-0000-0001-000000000001', 'admin@spazasure.co.za', '+27810000001',
       '$2b$10$KJYHL0OnwL/dVv5PiNW36eUn3r6ilXPDcr1TEwoTTLqNTJQdSmILO',
       (SELECT id FROM roles WHERE name = 'admin'), 'active'
WHERE NOT EXISTS (SELECT 1 FROM users WHERE email = 'admin@spazasure.co.za');

-- Databases seeded before the rename get the new supplier login on the same user row
UPDATE users SET email = 'supplier@spazasure.co.za'
WHERE id = '00000000-0000-0000-0002-000000000001' AND email = 'thabo@freshfoods.co.za';

INSERT INTO users (id, email, phone, password_hash, role_id, status)
SELECT '00000000-0000-0000-0002-000000000001', 'supplier@spazasure.co.za', '+27810000002',
       '$2b$10$KJYHL0OnwL/dVv5PiNW36eUn3r6ilXPDcr1TEwoTTLqNTJQdSmILO',
       (SELECT id FROM roles WHERE name = 'supplier'), 'active'
WHERE NOT EXISTS (SELECT 1 FROM users WHERE id = '00000000-0000-0000-0002-000000000001' OR email = 'supplier@spazasure.co.za');

INSERT INTO users (id, email, phone, password_hash, role_id, status)
SELECT '00000000-0000-0000-0004-000000000001', 'sipho@cornershop.co.za', '+27810000003',
       '$2b$10$KJYHL0OnwL/dVv5PiNW36eUn3r6ilXPDcr1TEwoTTLqNTJQdSmILO',
       (SELECT id FROM roles WHERE name = 'spaza_owner'), 'active'
WHERE NOT EXISTS (SELECT 1 FROM users WHERE email = 'sipho@cornershop.co.za');

INSERT INTO users (id, email, phone, password_hash, role_id, status)
SELECT '00000000-0000-0000-0008-000000000001', 'zanele@example.co.za', '+27810000004',
       '$2b$10$KJYHL0OnwL/dVv5PiNW36eUn3r6ilXPDcr1TEwoTTLqNTJQdSmILO',
       (SELECT id FROM roles WHERE name = 'customer'), 'active'
WHERE NOT EXISTS (SELECT 1 FROM users WHERE email = 'zanele@example.co.za');

INSERT INTO customer_profiles (user_id, first_name, last_name, allergies)
SELECT (SELECT id FROM users WHERE email = 'zanele@example.co.za'), 'Zanele', 'Mthembu', '["Peanuts","Dairy"]'
WHERE NOT EXISTS (
    SELECT 1 FROM customer_profiles WHERE user_id = (SELECT id FROM users WHERE email = 'zanele@example.co.za')
);

-- =========================================================================
-- 2. Supplier record (kept at its original fixed ID so anything else in the
--    project referencing 00000000-0000-0000-0003-000000000001 still works).
-- =========================================================================

INSERT INTO suppliers (
    id, user_id, company_name, registration_number, vat_number, contact_person,
    phone, email, address, city, province, tier, status, commission_rate
)
SELECT
    '00000000-0000-0000-0003-000000000001',
    (SELECT id FROM users WHERE email = 'supplier@spazasure.co.za'),
    'SpazaSure (Pty) Ltd', '2020/123456/07', '4123456789', 'SpazaSure Supply Team',
    '+27810000002', 'supplier@spazasure.co.za', '12 Industrial Road, Johannesburg',
    'Johannesburg', 'Gauteng', 'silver', 'verified', 3.00
WHERE NOT EXISTS (SELECT 1 FROM suppliers WHERE id = '00000000-0000-0000-0003-000000000001');

-- Keep it up to date if it already existed from a previous partial run.
UPDATE suppliers SET
  company_name = 'SpazaSure (Pty) Ltd',
  contact_person = 'SpazaSure Supply Team',
  email = 'supplier@spazasure.co.za',
  status = 'verified',
  tier = 'silver',
  commission_rate = 3.00,
  city = 'Johannesburg',
  province = 'Gauteng',
  address = '12 Industrial Road, Johannesburg'
WHERE id = '00000000-0000-0000-0003-000000000001';

-- Approve compliance documents so the Supplier Portal's Products/Orders
-- pages aren't gated behind the "Documents Required" modal during testing.
INSERT INTO supplier_documents (supplier_id, doc_type, doc_url, status, verified_at)
SELECT '00000000-0000-0000-0003-000000000001', d.doc_type, '/uploads/demo/' || d.doc_type || '.pdf', 'approved', NOW()
FROM (VALUES ('cipc_certificate'), ('tax_clearance'), ('bee_certificate'), ('product_license')) AS d(doc_type)
WHERE NOT EXISTS (
    SELECT 1 FROM supplier_documents
    WHERE supplier_id = '00000000-0000-0000-0003-000000000001' AND doc_type = d.doc_type
);

-- =========================================================================
-- 3. One spaza shop, to order from the supplier.
-- =========================================================================

INSERT INTO spaza_shops (
    id, user_id, shop_name, owner_name, phone, email, address, city, province,
    status, compliance_status, onboarding_fee_paid
)
SELECT
    '00000000-0000-0000-0005-000000000001',
    (SELECT id FROM users WHERE email = 'sipho@cornershop.co.za'),
    'Sipho''s Corner Shop', 'Sipho Dlamini', '+27810000003', 'sipho@cornershop.co.za',
    '45 Vilakazi Street, Soweto', 'Johannesburg', 'Gauteng',
    'active', 'compliant', true
WHERE NOT EXISTS (SELECT 1 FROM spaza_shops WHERE id = '00000000-0000-0000-0005-000000000001');

-- =========================================================================
-- 4. SpazaSure's product catalogue.
-- =========================================================================

INSERT INTO products (id, supplier_id, category_id, name, description, sku, price, stock_qty, min_order_qty, unit, images, is_available, is_approved, is_food_item, created_at, updated_at)
SELECT gen_random_uuid(), '00000000-0000-0000-0003-000000000001', (SELECT id FROM categories WHERE slug = 'maize-flour'), 'White Star Maize Meal 10kg', 'Premium super maize meal', 'WS-MM-10', 89.99, 500, 10, 'bag', '[]', true, true, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM products WHERE sku = 'WS-MM-10');

INSERT INTO products (id, supplier_id, category_id, name, description, sku, price, stock_qty, min_order_qty, unit, images, is_available, is_approved, is_food_item, created_at, updated_at)
SELECT gen_random_uuid(), '00000000-0000-0000-0003-000000000001', (SELECT id FROM categories WHERE slug = 'maize-flour'), 'Tastic Rice 2kg', 'Long grain parboiled rice', 'TR-2K', 42.99, 300, 12, 'pack', '[]', true, true, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM products WHERE sku = 'TR-2K');

INSERT INTO products (id, supplier_id, category_id, name, description, sku, price, stock_qty, min_order_qty, unit, images, is_available, is_approved, is_food_item, created_at, updated_at)
SELECT gen_random_uuid(), '00000000-0000-0000-0003-000000000001', (SELECT id FROM categories WHERE slug = 'cooking-oil'), 'Sunfoil Cooking Oil 2L', 'Pure sunflower cooking oil', 'SF-CO-2L', 54.99, 200, 6, 'bottle', '[]', true, true, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM products WHERE sku = 'SF-CO-2L');

INSERT INTO products (id, supplier_id, category_id, name, description, sku, price, stock_qty, min_order_qty, unit, images, is_available, is_approved, is_food_item, created_at, updated_at)
SELECT gen_random_uuid(), '00000000-0000-0000-0003-000000000001', (SELECT id FROM categories WHERE slug = 'beverages'), 'Coca-Cola 2L (6-Pack)', 'Original taste. 6 bottles', 'CC-2L-6', 89.99, 150, 4, 'pack', '[]', true, true, false, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM products WHERE sku = 'CC-2L-6');

INSERT INTO products (id, supplier_id, category_id, name, description, sku, price, stock_qty, min_order_qty, unit, images, is_available, is_approved, is_food_item, created_at, updated_at)
SELECT gen_random_uuid(), '00000000-0000-0000-0003-000000000001', (SELECT id FROM categories WHERE slug = 'snacks'), 'Simba Chips Assorted 36s', 'Mixed flavour chips', 'SC-36', 149.99, 100, 2, 'box', '[]', true, true, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM products WHERE sku = 'SC-36');

INSERT INTO products (id, supplier_id, category_id, name, description, sku, price, stock_qty, min_order_qty, unit, images, is_available, is_approved, is_food_item, created_at, updated_at)
SELECT gen_random_uuid(), '00000000-0000-0000-0003-000000000001', (SELECT id FROM categories WHERE slug = 'canned-goods'), 'Lucky Star Pilchards 400g (24-Pack)', 'Pilchards in tomato sauce', 'LS-400-24', 299.99, 60, 1, 'box', '[]', true, true, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM products WHERE sku = 'LS-400-24');

INSERT INTO products (id, supplier_id, category_id, name, description, sku, price, stock_qty, min_order_qty, unit, images, is_available, is_approved, is_food_item, created_at, updated_at)
SELECT gen_random_uuid(), '00000000-0000-0000-0003-000000000001', (SELECT id FROM categories WHERE slug = 'household-cleaning'), 'Sunlight Dishwashing 750ml (12-Pack)', 'Grease-cutting formula', 'SDL-750-12', 179.99, 120, 2, 'box', '[]', true, true, false, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM products WHERE sku = 'SDL-750-12');

INSERT INTO products (id, supplier_id, category_id, name, description, sku, price, stock_qty, min_order_qty, unit, images, is_available, is_approved, is_food_item, created_at, updated_at)
SELECT gen_random_uuid(), '00000000-0000-0000-0003-000000000001', (SELECT id FROM categories WHERE slug = 'bread-bakery'), 'Albany Bread (20 Loaves)', 'White sliced bread bulk', 'AB-WB-20', 259.99, 30, 1, 'case', '[]', true, true, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM products WHERE sku = 'AB-WB-20');

INSERT INTO products (id, supplier_id, category_id, name, description, sku, price, stock_qty, min_order_qty, unit, images, is_available, is_approved, is_food_item, created_at, updated_at)
SELECT gen_random_uuid(), '00000000-0000-0000-0003-000000000001', (SELECT id FROM categories WHERE slug = 'canned-goods'), 'KOO Baked Beans 410g (12-Pack)', 'Rich tomato sauce beans', 'KOO-BB-12', 129.99, 200, 2, 'case', '[]', true, true, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM products WHERE sku = 'KOO-BB-12');

INSERT INTO products (id, supplier_id, category_id, name, description, sku, price, stock_qty, min_order_qty, unit, images, is_available, is_approved, is_food_item, created_at, updated_at)
SELECT gen_random_uuid(), '00000000-0000-0000-0003-000000000001', (SELECT id FROM categories WHERE slug = 'dairy-eggs'), 'Clover Fresh Milk 2L (6-Pack)', 'Full cream fresh milk', 'CM-2L-6', 119.99, 80, 2, 'pack', '[]', true, true, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM products WHERE sku = 'CM-2L-6');

-- Tag a couple of products with allergens and a real barcode, so the
-- Customer app's Verify flow (and allergen warnings) have something to
-- demonstrate against.
UPDATE products SET barcode = '6001087340127', allergens = '["dairy"]' WHERE sku = 'CM-2L-6';
UPDATE products SET barcode = '6001240521003', allergens = '[]' WHERE sku = 'WS-MM-10';

-- A QR-coded batch of the milk, flagged as recalled, so a customer scanning
-- it sees a real recall warning rather than only the happy path.
INSERT INTO product_qr_codes (product_id, qr_code, batch_number, expiry_date, is_recalled)
SELECT (SELECT id FROM products WHERE sku = 'CM-2L-6'), 'SPZ-QR-CM2L6-0001', 'B2026-08-14', '2026-09-20', true
WHERE NOT EXISTS (SELECT 1 FROM product_qr_codes WHERE qr_code = 'SPZ-QR-CM2L6-0001');

-- =========================================================================
-- 5. Subscription plans (Basic/Bronze/Silver/Gold, matching the tier
--    comparison shown in the Supplier Portal's Analytics page). Without
--    these, GET /supplier/subscription/plans returns an empty list on a
--    fresh database and the Upgrade page has nothing to show.
-- =========================================================================

INSERT INTO subscription_plans (tier, name, description, monthly_price, annual_price, commission_rate, max_listings, max_orders, has_analytics, has_priority_support, has_bulk_pricing, has_api_access, has_custom_branding, sort_order)
SELECT 'basic', 'Basic', 'Get started on SpazaSure', 0, 0, 5.00, 10, 50, false, false, false, false, false, 1
WHERE NOT EXISTS (SELECT 1 FROM subscription_plans WHERE tier = 'basic');

INSERT INTO subscription_plans (tier, name, description, monthly_price, annual_price, commission_rate, max_listings, max_orders, has_analytics, has_priority_support, has_bulk_pricing, has_api_access, has_custom_branding, sort_order)
SELECT 'bronze', 'Bronze', 'For growing suppliers', 500, 5000, 4.00, 50, 250, true, false, false, false, false, 2
WHERE NOT EXISTS (SELECT 1 FROM subscription_plans WHERE tier = 'bronze');

INSERT INTO subscription_plans (tier, name, description, monthly_price, annual_price, commission_rate, max_listings, max_orders, has_analytics, has_priority_support, has_bulk_pricing, has_api_access, has_custom_branding, sort_order)
SELECT 'silver', 'Silver', 'For established suppliers', 1500, 15000, 3.00, 200, 1000, true, true, true, false, false, 3
WHERE NOT EXISTS (SELECT 1 FROM subscription_plans WHERE tier = 'silver');

INSERT INTO subscription_plans (tier, name, description, monthly_price, annual_price, commission_rate, max_listings, max_orders, has_analytics, has_priority_support, has_bulk_pricing, has_api_access, has_custom_branding, sort_order)
SELECT 'gold', 'Gold', 'For high-volume suppliers', 5500, 55000, 2.00, 1000, 10000, true, true, true, true, true, 4
WHERE NOT EXISTS (SELECT 1 FROM subscription_plans WHERE tier = 'gold');

COMMIT;
