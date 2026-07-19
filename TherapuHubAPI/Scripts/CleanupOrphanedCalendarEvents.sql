-- ============================================================================
-- CleanupOrphanedCalendarEvents.sql
--
-- Limpieza de eventos de calendario (tabla "Events") realmente huerfanos:
-- casos en que el Staff o Cliente dueno del cumpleanos YA NO EXISTE en la
-- base de datos (fue borrado fisicamente). Ver ValidateOrphanedCalendarEvents.sql
-- para el diagnostico completo antes de correr esto.
--
-- IMPORTANTE:
--   - Este script SOLO borra eventos huerfanos reales (FK "colgada", el
--     Staff/Cliente ya no existe). NO borra eventos de Staff/Clientes que
--     solo estan DESACTIVADOS (Actor.IsActive = false) -- desactivar no es
--     lo mismo que eliminar, y ese caso se revisa aparte, a criterio del
--     cliente (ver seccion 2 y 4 del script de validacion).
--   - "Events" no tiene soft-delete propio, asi que este DELETE es fisico
--     (irreversible) para la tabla Events -- no hay a donde "restaurar".
--   - Corre dentro de una transaccion: revisa los SELECT del "antes" y el
--     conteo de filas afectadas, y decide COMMIT o ROLLBACK a mano.
--     NO tiene COMMIT automatico al final a proposito.
-- ============================================================================

BEGIN;

-- --- Antes de borrar: snapshot de lo que se va a eliminar -------------------
SELECT 'A eliminar: cumpleanos de Staff ya inexistente' AS "Detalle",
       e."Id", e."Title", e."StartDate", e."CompanyId", e."StaffId"
FROM "Events" e
WHERE e."StaffId" IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM "Staff" s WHERE s."Id" = e."StaffId");

SELECT 'A eliminar: cumpleanos de Cliente ya inexistente' AS "Detalle",
       e."Id", e."Title", e."StartDate", e."CompanyId", e."OtherType"
FROM "Events" e
WHERE e."OtherType" LIKE 'BIRTHDAY:%'
  AND NOT EXISTS (
      SELECT 1 FROM "Clients" c
      WHERE c."Id" = (regexp_match(e."OtherType", '^BIRTHDAY:(\d+)$'))[1]::int
  );

SELECT 'A eliminar: EventUsers de Usuario inexistente' AS "Detalle",
       eu."Id", eu."EventId", eu."UserId"
FROM "EventUsers" eu
WHERE NOT EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = eu."UserId");

-- --- Borrado ------------------------------------------------------------

-- 1) EventUsers que apuntan a un Usuario que ya no existe (huerfano real).
--    Se borra primero por si mas adelante se agrega una FK real EventUsers->Events.
DELETE FROM "EventUsers" eu
WHERE NOT EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = eu."UserId");

-- 2) Cumpleanos de Staff cuyo registro Staff ya no existe.
DELETE FROM "Events" e
WHERE e."StaffId" IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM "Staff" s WHERE s."Id" = e."StaffId");

-- 3) Cumpleanos de Cliente cuyo registro Cliente ya no existe.
DELETE FROM "Events" e
WHERE e."OtherType" LIKE 'BIRTHDAY:%'
  AND NOT EXISTS (
      SELECT 1 FROM "Clients" c
      WHERE c."Id" = (regexp_match(e."OtherType", '^BIRTHDAY:(\d+)$'))[1]::int
  );

-- Revisa el resultado de los SELECT de arriba y el numero de filas afectadas
-- por cada DELETE. Si todo se ve correcto:
--   COMMIT;
-- Si algo no cuadra:
--   ROLLBACK;
