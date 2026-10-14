-- PostgreSQL
-- Auto-assignment of all RBTs (Supervisor → RBT, RelationshipTypeId = 1) by user type.
--   UserTypes.AutoAssignAllRbts        : when true, users of this type are linked to every RBT in their company.
--   ActorRelationships.IsAutoAssigned  : true when the relationship was created by the system (not by a user),
--                                        so it can be removed when the flag is turned off without touching manual ones.

BEGIN;

ALTER TABLE "UserTypes"
    ADD COLUMN IF NOT EXISTS "AutoAssignAllRbts" boolean NOT NULL DEFAULT false;

ALTER TABLE "ActorRelationships"
    ADD COLUMN IF NOT EXISTS "IsAutoAssigned" boolean NOT NULL DEFAULT false;

-- Remove exact duplicates (same source, target and type), keeping the oldest row,
-- so the unique index below can be created.
DELETE FROM "ActorRelationships" r
USING "ActorRelationships" d
WHERE r."SourceActorId" = d."SourceActorId"
  AND r."TargetActorId" = d."TargetActorId"
  AND r."RelationshipTypeId" = d."RelationshipTypeId"
  AND r."Id" > d."Id";

CREATE UNIQUE INDEX IF NOT EXISTS "UX_ActorRelationships_Source_Target_Type"
    ON "ActorRelationships" ("SourceActorId", "TargetActorId", "RelationshipTypeId");

COMMIT;
