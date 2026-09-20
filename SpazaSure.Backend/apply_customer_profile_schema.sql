-- Repairs production databases created before customer first/last name fields
-- were introduced. Safe to run more than once.

BEGIN;

ALTER TABLE customer_profiles ADD COLUMN IF NOT EXISTS first_name text;
ALTER TABLE customer_profiles ADD COLUMN IF NOT EXISTS last_name text;
ALTER TABLE customer_profiles ADD COLUMN IF NOT EXISTS age integer;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'customer_profiles' AND column_name = 'full_name'
    ) THEN
        EXECUTE $sql$
            UPDATE customer_profiles
            SET
                first_name = COALESCE(NULLIF(first_name, ''), split_part(full_name, ' ', 1), ''),
                last_name = COALESCE(NULLIF(last_name, ''), NULLIF(trim(substr(full_name, length(split_part(full_name, ' ', 1)) + 1)), ''), '')
            WHERE first_name IS NULL OR last_name IS NULL
        $sql$;
    END IF;
END $$;

ALTER TABLE customer_profiles ALTER COLUMN first_name SET DEFAULT '';
ALTER TABLE customer_profiles ALTER COLUMN last_name SET DEFAULT '';
UPDATE customer_profiles SET first_name = '' WHERE first_name IS NULL;
UPDATE customer_profiles SET last_name = '' WHERE last_name IS NULL;
ALTER TABLE customer_profiles ALTER COLUMN first_name SET NOT NULL;
ALTER TABLE customer_profiles ALTER COLUMN last_name SET NOT NULL;

COMMIT;