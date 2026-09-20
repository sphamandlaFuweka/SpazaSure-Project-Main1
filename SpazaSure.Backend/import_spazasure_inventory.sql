-- Imports the supplied products (1).csv inventory into SpazaSure (Pty) Ltd.
-- Safe to re-run: supplier SKU is the source barcode and conflicts update stock/price.
-- Run against the intended PostgreSQL database only after confirming the supplier name.

BEGIN;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM suppliers WHERE company_name = 'SpazaSure (Pty) Ltd'
    ) THEN
        RAISE EXCEPTION 'Supplier "SpazaSure (Pty) Ltd" was not found. Create or rename the supplier before importing.';
    END IF;
END $$;

WITH source(name, brand, barcode, price, stock_qty) AS (
    VALUES
        ('NICE AND FRESH BISCUITS 4KG', 'NICE AND FRESH', '100010002526', 114.99, 20),
        ('SWITCH 24X500ML VIT C', 'SWITCH', '100200105760', 169.99, 17),
        ('DRAGON 6X500ML ORIGINAL', 'DRAGON', '100010002920', 46.98, 19),
        ('SUNLIGHT SOAP 12X125GM', 'SUNLIGHT', '100010001834', 79.99, 19),
        ('HULETTS WHITE SUGAR 5X1KG', 'HULETTS', '100010001543', 116.99, 20),
        ('JABA CHICKEN STOCK 254 SACHETS', 'JABA', '100010002385', 25.99, 19),
        ('Omo 2KG x6', 'OMO', '1255698855', 165.00, 14),
        ('Sunlight Dishwashing Liquid 6x400ml', 'Sunlight', '6001087340016', 119.99, 140),
        ('Coca-Cola 12x1.5ml', 'Coca-Cola', '5449000000996', 199.99, 333),
        ('Coca-Cola 6X2.25L', 'Coca-Cola', '100200115062', 134.99, 20),
        ('NOVA BUCKET BISCUIT', 'Nova', '6001087123456', 106.99, 20),
        ('AZAM FLOUR 10KG', 'Azam', '123', 99.99, 20),
        ('STYLOS SWEET CHILLI 10X125G', 'STYLOS', '6003515700012', 54.99, 20),
        ('Pin Pop lollipopb Flavours', 'Pin Pop', '100010003053', 39.99, 20),
        ('Beacon 50''s Mint Smoothies', 'Beacon', '100010003070', 17.99, 20),
        ('EEZEE Noodles flavours 5x65g', 'EEZEE', '100200116059', 39.89, 20),
        ('Thursti Water 6x1.5 L Naarjie', 'Thursti Water', '100200106120', 139.98, 20),
        ('HHM sliced polony Chicken 1KG', 'HHM', '100010003588', 49.99, 20),
        ('KNORROX JAR144 CHBEEF', 'Knorr', '100010002369', 89.99, 20),
        ('GO SLO12X100GM CHONION', 'GO SLO', '009803631699', 49.99, 20),
        ('HALLS XTRA STRONG 72''s', 'HALLS', '100010004697', 18.50, 20),
        ('STIMOROL AIR RUSH 50''s', 'STIMOROL', '100010002962', 42.50, 20),
        ('ZAH POTATOES', 'ZAH', '100200102928', 65.00, 20),
        ('ZAH TOMATO BOX', 'ZAH', '100200102930', 85.00, 20),
        ('LEMON TWIST 12X1.5 L', 'LEMON TWIST', '100010001248', 167.97, 20),
        ('TOPPER 12X125G CHOCOLATE', 'TOPPER', '100010002459', 215.98, 20),
        ('NCP YEAST 500G', 'N.C.P', '100010003196', 46.99, 20),
        ('ZAH ONIONS', 'ZAH', '100300102989', 95.00, 20),
        ('HULLETS BROWN SUGAR 5X1KG', 'HULLETS', '100010001548', 107.99, 20),
        ('sugar', 'hullets', '1529269996665', 200.00, 13),
        ('SUNLIGHT W/P 6X250G', 'SUNLIGHT', '100010001797', 65.99, 13),
        ('AZAM FLOUR 10KG', 'Azam', '1111111111', 100.00, 7),
        ('CLOVER STERILE MILK 6X300ML', 'CLOVER', '100010003458', 65.99, 2),
        ('OMO W/P 6X250G', 'OMO', '100010001799', 68.99, 13),
        ('CLOVER FULL CREAM MILK 6X1L', 'CLOVER', '100010003452', 104.99, 19),
        ('DAISY SOFT TOILET TISSUE 10''s', 'DAISY', '100010001354', 27.99, 19),
        ('GRAND PA 38''s', 'GRAND PA', '100010001140', 81.99, 39)
), target AS (
    SELECT
        (SELECT id FROM suppliers WHERE company_name = 'SpazaSure (Pty) Ltd') AS supplier_id,
        (SELECT id FROM categories WHERE slug = 'groceries' LIMIT 1) AS category_id
)
INSERT INTO products (
    id, supplier_id, category_id, name, description, sku, barcode, price,
    stock_qty, min_order_qty, unit, images, is_available, is_approved,
    is_food_item, created_at, updated_at
)
SELECT
    gen_random_uuid(),
    target.supplier_id,
    target.category_id,
    source.name,
    source.brand || ' product',
    source.barcode,
    source.barcode,
    source.price,
    source.stock_qty,
    1,
    'unit',
    '[]',
    true,
    true,
    true,
    NOW(),
    NOW()
FROM source
CROSS JOIN target
ON CONFLICT (supplier_id, sku) DO UPDATE SET
    name = EXCLUDED.name,
    description = EXCLUDED.description,
    barcode = EXCLUDED.barcode,
    price = EXCLUDED.price,
    stock_qty = EXCLUDED.stock_qty,
    category_id = EXCLUDED.category_id,
    is_available = true,
    is_approved = true,
    updated_at = NOW();

COMMIT;
