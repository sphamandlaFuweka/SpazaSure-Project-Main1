-- ============================================================================
-- Move barcodes that were typed into the SKU field into the barcode field.
--
-- Suppliers had no barcode field, so they typed the number printed on the pack
-- into SKU, and SpazaSure generated a different barcode. Scanning the real pack
-- then found nothing. For every product whose SKU is only a number of 8 to 14
-- digits (spaces and dashes ignored), this copies it into the barcode, replacing
-- the generated one, and leaves the SKU empty.
--
-- Run AFTER deploying the backend: the new migrate.sql lets several products of
-- one supplier have an empty SKU. The script stops (changing nothing) otherwise.
--
-- Run on the server:
--   docker exec -i spazasure-prod-db psql -U spazasure_prod_user -d spazasure -v ON_ERROR_STOP=1 < move_sku_to_barcode.sql
-- ============================================================================
BEGIN;

DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'products'
             AND indexname IN ('IX_Products_SupplierId_Sku', 'ix_products_supplier_id_sku')) THEN
    RAISE EXCEPTION 'Deploy the backend first: the old unique SKU index is still in place, so products cannot share an empty SKU yet.';
  END IF;
END $$;

-- What will change
SELECT 'will move' AS stage, s.company_name AS supplier, p.name AS product,
       p.sku AS old_sku, p.barcode AS old_barcode,
       regexp_replace(p.sku, '[\s-]', '', 'g') AS new_barcode
FROM products p JOIN suppliers s ON s.id = p.supplier_id
WHERE regexp_replace(p.sku, '[\s-]', '', 'g') ~ '^[0-9]{8,14}$'
ORDER BY s.company_name, p.name;

-- Numbers that are too short or long to be a barcode stay as SKUs; listed to check by hand
SELECT 'left as SKU' AS stage, s.company_name AS supplier, p.name AS product, p.sku
FROM products p JOIN suppliers s ON s.id = p.supplier_id
WHERE regexp_replace(p.sku, '[\s-]', '', 'g') ~ '^[0-9]+$'
  AND regexp_replace(p.sku, '[\s-]', '', 'g') !~ '^[0-9]{8,14}$'
ORDER BY s.company_name, p.name;

UPDATE products
SET barcode = regexp_replace(sku, '[\s-]', '', 'g'),
    sku = '',
    updated_at = NOW()
WHERE regexp_replace(sku, '[\s-]', '', 'g') ~ '^[0-9]{8,14}$';

SELECT 'after' AS stage,
       (SELECT count(*) FROM products WHERE sku = '') AS products_without_sku,
       (SELECT count(*) FROM products WHERE barcode LIKE '290%') AS spazasure_barcodes;

COMMIT;
