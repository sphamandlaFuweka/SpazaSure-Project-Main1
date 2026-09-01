-- SpazaSure demo/local seed data
-- Run AFTER migrate.sql (which creates the schema plus baseline roles/categories).
-- Safe to re-run: every statement is idempotent (WHERE NOT EXISTS / ON CONFLICT).
--
-- Demo login credentials (password is the same for all three, for convenience):
--   Admin:     admin@spazasure.co.za    / Password123!
--   Supplier:  thabo@freshfoods.co.za   / Password123!
--   Shop owner: sipho@cornershop.co.za  / Password123!
--
-- The password hash below is a bcrypt hash of "Password123!" (cost 10),
-- compatible with BCrypt.Net-Next as used in AuthenticationService.cs.

BEGIN;

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

INSERT INTO users (id, email, phone, password_hash, role_id, status)
SELECT '00000000-0000-0000-0002-000000000001', 'thabo@freshfoods.co.za', '+27810000002',
       '$2b$10$KJYHL0OnwL/dVv5PiNW36eUn3r6ilXPDcr1TEwoTTLqNTJQdSmILO',
       (SELECT id FROM roles WHERE name = 'supplier'), 'active'
WHERE NOT EXISTS (SELECT 1 FROM users WHERE email = 'thabo@freshfoods.co.za');

INSERT INTO users (id, email, phone, password_hash, role_id, status)
SELECT '00000000-0000-0000-0004-000000000001', 'sipho@cornershop.co.za', '+27810000003',
       '$2b$10$KJYHL0OnwL/dVv5PiNW36eUn3r6ilXPDcr1TEwoTTLqNTJQdSmILO',
       (SELECT id FROM roles WHERE name = 'spaza_owner'), 'active'
WHERE NOT EXISTS (SELECT 1 FROM users WHERE email = 'sipho@cornershop.co.za');

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
    (SELECT id FROM users WHERE email = 'thabo@freshfoods.co.za'),
    'Fresh Foods SA (Pty) Ltd', '2020/123456/07', '4123456789', 'Thabo Nkosi',
    '+27810000002', 'thabo@freshfoods.co.za', '12 Industrial Road, Johannesburg',
    'Johannesburg', 'Gauteng', 'silver', 'verified', 3.00
WHERE NOT EXISTS (SELECT 1 FROM suppliers WHERE id = '00000000-0000-0000-0003-000000000001');

-- Keep it up to date if it already existed from a previous partial run.
UPDATE suppliers SET
  company_name = 'Fresh Foods SA (Pty) Ltd',
  contact_person = 'Thabo Nkosi',
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
-- 3. A spaza shop (buyer side) so Orders/Group Buy have a realistic
--    counterparty instead of dangling FK references.
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
-- 4. Product catalog for the demo supplier.
-- =========================================================================

INSERT INTO products (id, supplier_id, category_id, name, description, sku, price, stock_qty, min_order_qty, unit, images, is_available, is_approved, is_food_item, created_at, updated_at)
SELECT gen_random_uuid(), '00000000-0000-0000-0003-000000000001', (SELECT id FROM categories LIMIT 1), 'White Star Maize Meal 10kg', 'Premium super maize meal', 'WS-MM-10', 89.99, 500, 10, 'bag', '[]', true, true, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM products WHERE sku = 'WS-MM-10');

INSERT INTO products (id, supplier_id, category_id, name, description, sku, price, stock_qty, min_order_qty, unit, images, is_available, is_approved, is_food_item, created_at, updated_at)
SELECT gen_random_uuid(), '00000000-0000-0000-0003-000000000001', (SELECT id FROM categories LIMIT 1), 'Tastic Rice 2kg', 'Long grain parboiled rice', 'TR-2K', 42.99, 300, 12, 'pack', '[]', true, true, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM products WHERE sku = 'TR-2K');

INSERT INTO products (id, supplier_id, category_id, name, description, sku, price, stock_qty, min_order_qty, unit, images, is_available, is_approved, is_food_item, created_at, updated_at)
SELECT gen_random_uuid(), '00000000-0000-0000-0003-000000000001', (SELECT id FROM categories LIMIT 1), 'Sunfoil Cooking Oil 2L', 'Pure sunflower cooking oil', 'SF-CO-2L', 54.99, 200, 6, 'bottle', '[]', true, true, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM products WHERE sku = 'SF-CO-2L');

INSERT INTO products (id, supplier_id, category_id, name, description, sku, price, stock_qty, min_order_qty, unit, images, is_available, is_approved, is_food_item, created_at, updated_at)
SELECT gen_random_uuid(), '00000000-0000-0000-0003-000000000001', (SELECT id FROM categories LIMIT 1), 'Coca-Cola 2L (6-Pack)', 'Original taste. 6 bottles', 'CC-2L-6', 89.99, 150, 4, 'pack', '[]', true, true, false, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM products WHERE sku = 'CC-2L-6');

INSERT INTO products (id, supplier_id, category_id, name, description, sku, price, stock_qty, min_order_qty, unit, images, is_available, is_approved, is_food_item, created_at, updated_at)
SELECT gen_random_uuid(), '00000000-0000-0000-0003-000000000001', (SELECT id FROM categories LIMIT 1), 'Simba Chips Assorted 36s', 'Mixed flavour chips', 'SC-36', 149.99, 100, 2, 'box', '[]', true, true, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM products WHERE sku = 'SC-36');

INSERT INTO products (id, supplier_id, category_id, name, description, sku, price, stock_qty, min_order_qty, unit, images, is_available, is_approved, is_food_item, created_at, updated_at)
SELECT gen_random_uuid(), '00000000-0000-0000-0003-000000000001', (SELECT id FROM categories LIMIT 1), 'Lucky Star Pilchards 400g (24-Pack)', 'Pilchards in tomato sauce', 'LS-400-24', 299.99, 60, 1, 'box', '[]', true, true, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM products WHERE sku = 'LS-400-24');

INSERT INTO products (id, supplier_id, category_id, name, description, sku, price, stock_qty, min_order_qty, unit, images, is_available, is_approved, is_food_item, created_at, updated_at)
SELECT gen_random_uuid(), '00000000-0000-0000-0003-000000000001', (SELECT id FROM categories LIMIT 1), 'Sunlight Dishwashing 750ml (12-Pack)', 'Grease-cutting formula', 'SDL-750-12', 179.99, 120, 2, 'box', '[]', true, true, false, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM products WHERE sku = 'SDL-750-12');

INSERT INTO products (id, supplier_id, category_id, name, description, sku, price, stock_qty, min_order_qty, unit, images, is_available, is_approved, is_food_item, created_at, updated_at)
SELECT gen_random_uuid(), '00000000-0000-0000-0003-000000000001', (SELECT id FROM categories LIMIT 1), 'Albany Bread (20 Loaves)', 'White sliced bread bulk', 'AB-WB-20', 259.99, 30, 1, 'case', '[]', true, true, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM products WHERE sku = 'AB-WB-20');

INSERT INTO products (id, supplier_id, category_id, name, description, sku, price, stock_qty, min_order_qty, unit, images, is_available, is_approved, is_food_item, created_at, updated_at)
SELECT gen_random_uuid(), '00000000-0000-0000-0003-000000000001', (SELECT id FROM categories LIMIT 1), 'KOO Baked Beans 410g (12-Pack)', 'Rich tomato sauce beans', 'KOO-BB-12', 129.99, 200, 2, 'case', '[]', true, true, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM products WHERE sku = 'KOO-BB-12');

INSERT INTO products (id, supplier_id, category_id, name, description, sku, price, stock_qty, min_order_qty, unit, images, is_available, is_approved, is_food_item, created_at, updated_at)
SELECT gen_random_uuid(), '00000000-0000-0000-0003-000000000001', (SELECT id FROM categories LIMIT 1), 'Clover Fresh Milk 2L (6-Pack)', 'Full cream fresh milk', 'CM-2L-6', 119.99, 80, 2, 'pack', '[]', true, true, true, NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM products WHERE sku = 'CM-2L-6');

-- =========================================================================
-- 5. A handful of sample orders across different statuses, so the Orders /
--    Analytics / Dashboard pages have something real to render instead of
--    empty states on a fresh database.
-- =========================================================================

INSERT INTO orders (id, order_number, shop_id, supplier_id, status, subtotal, delivery_fee, platform_commission, total_amount, payment_status, created_at)
SELECT '00000000-0000-0000-0006-000000000001', 'ORD-1001',
       '00000000-0000-0000-0005-000000000001', '00000000-0000-0000-0003-000000000001',
       'pending', 899.90, 50.00, 26.99, 949.90, 'pending', NOW() - INTERVAL '1 day'
WHERE NOT EXISTS (SELECT 1 FROM orders WHERE order_number = 'ORD-1001');

INSERT INTO orders (id, order_number, shop_id, supplier_id, status, subtotal, delivery_fee, platform_commission, total_amount, payment_status, created_at)
SELECT '00000000-0000-0000-0006-000000000002', 'ORD-1002',
       '00000000-0000-0000-0005-000000000001', '00000000-0000-0000-0003-000000000001',
       'processing', 429.90, 50.00, 12.90, 479.90, 'paid', NOW() - INTERVAL '2 days'
WHERE NOT EXISTS (SELECT 1 FROM orders WHERE order_number = 'ORD-1002');

INSERT INTO orders (id, order_number, shop_id, supplier_id, status, subtotal, delivery_fee, platform_commission, total_amount, payment_status, created_at)
SELECT '00000000-0000-0000-0006-000000000003', 'ORD-1003',
       '00000000-0000-0000-0005-000000000001', '00000000-0000-0000-0003-000000000001',
       'delivered', 259.99, 50.00, 7.80, 309.99, 'paid', NOW() - INTERVAL '5 days'
WHERE NOT EXISTS (SELECT 1 FROM orders WHERE order_number = 'ORD-1003');

INSERT INTO order_items (order_id, product_id, quantity, original_unit_price, unit_price, line_total)
SELECT '00000000-0000-0000-0006-000000000001', p.id, 10, p.price, p.price, p.price * 10
FROM products p WHERE p.sku = 'WS-MM-10'
AND NOT EXISTS (SELECT 1 FROM order_items WHERE order_id = '00000000-0000-0000-0006-000000000001' AND product_id = p.id);

INSERT INTO order_items (order_id, product_id, quantity, original_unit_price, unit_price, line_total)
SELECT '00000000-0000-0000-0006-000000000002', p.id, 10, p.price, p.price, p.price * 10
FROM products p WHERE p.sku = 'TR-2K'
AND NOT EXISTS (SELECT 1 FROM order_items WHERE order_id = '00000000-0000-0000-0006-000000000002' AND product_id = p.id);

INSERT INTO order_items (order_id, product_id, quantity, original_unit_price, unit_price, line_total)
SELECT '00000000-0000-0000-0006-000000000003', p.id, 1, p.price, p.price, p.price
FROM products p WHERE p.sku = 'AB-WB-20'
AND NOT EXISTS (SELECT 1 FROM order_items WHERE order_id = '00000000-0000-0000-0006-000000000003' AND product_id = p.id);

-- =========================================================================
-- 6. Subscription plans (Basic/Bronze/Silver/Gold, matching the tier
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

-- =========================================================================
-- 7. A sample group buy (Bulk Maize Meal), so the Group Buy page has
--    something real to render instead of an empty state on a fresh DB.
-- =========================================================================

INSERT INTO group_buys (id, title, description, product_id, supplier_id, target_qty, current_qty, original_price, discount_price, discount_pct, expires_at, status, created_by_shop_id)
SELECT
    '00000000-0000-0000-0007-000000000001',
    'Bulk Maize Meal Group Buy', 'Order together with other shops to unlock a bulk discount on 10kg bags.',
    (SELECT id FROM products WHERE sku = 'WS-MM-10'),
    '00000000-0000-0000-0003-000000000001',
    100, 20, 89.99, 76.49, 15,
    NOW() + INTERVAL '9 days', 'active',
    '00000000-0000-0000-0005-000000000001'
WHERE NOT EXISTS (SELECT 1 FROM group_buys WHERE id = '00000000-0000-0000-0007-000000000001');

INSERT INTO group_buy_participants (group_buy_id, shop_id, quantity, status)
SELECT '00000000-0000-0000-0007-000000000001', '00000000-0000-0000-0005-000000000001', 20, 'joined'
WHERE NOT EXISTS (
    SELECT 1 FROM group_buy_participants
    WHERE group_buy_id = '00000000-0000-0000-0007-000000000001' AND shop_id = '00000000-0000-0000-0005-000000000001'
);

COMMIT;
