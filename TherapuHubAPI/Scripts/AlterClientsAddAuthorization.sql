-- Script to add Authorization date range (start / end) to Clients table
-- Execute this script in the TherapuHub database (PostgreSQL)
-- Both columns are nullable: a client may have no authorization, or only one of the dates set.

ALTER TABLE "Clients"
    ADD COLUMN IF NOT EXISTS "AuthorizationStartDate" date NULL;

ALTER TABLE "Clients"
    ADD COLUMN IF NOT EXISTS "AuthorizationEndDate" date NULL;

-- Ensure end date is not before start date (only checked when both are set)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'CK_Clients_AuthorizationDates'
    ) THEN
        ALTER TABLE "Clients"
            ADD CONSTRAINT "CK_Clients_AuthorizationDates"
            CHECK ("AuthorizationStartDate" IS NULL
                OR "AuthorizationEndDate" IS NULL
                OR "AuthorizationEndDate" >= "AuthorizationStartDate");
        RAISE NOTICE 'Constraint CK_Clients_AuthorizationDates created.';
    ELSE
        RAISE NOTICE 'Constraint CK_Clients_AuthorizationDates already exists.';
    END IF;
END $$;

-- Rollback:
-- ALTER TABLE "Clients" DROP CONSTRAINT IF EXISTS "CK_Clients_AuthorizationDates";
-- ALTER TABLE "Clients" DROP COLUMN IF EXISTS "AuthorizationEndDate";
-- ALTER TABLE "Clients" DROP COLUMN IF EXISTS "AuthorizationStartDate";
