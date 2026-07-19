-- ============================================================================
-- ValidateOrphanedCalendarEvents.sql
--
-- Diagnostico de eventos de calendario (tabla "Events") huerfanos o asociados
-- a usuarios/staff/clientes inactivos o eliminados.
--
-- CONTEXTO (verificado en el codigo, no solo en la BD):
--   - "Events" NO tiene soft-delete propio (sin columnas IsDeleted/DeletedAt).
--   - "Events" NO tiene FK real a Staff/Clients/Users a nivel de base de datos
--     (StaffId es un int suelto, sin constraint). Por eso un DELETE en Staff
--     no falla ni arrastra nada en Events: el registro queda huerfano.
--   - StaffService.DeleteAsync hace DELETE fisico de Staff + Actor, pero NO
--     llama a ninguna limpieza de su evento de cumpleanos -> huerfano garantizado.
--   - ClientService.DeleteAsync SI limpia el evento de cumpleanos del cliente
--     (DeleteBirthdayEventAsync, via el tag "BIRTHDAY:{clientId}" en OtherType),
--     asi que en teoria no deberia dejar huerfanos -- lo validamos igual por si
--     hay eventos creados antes de ese fix o por edicion manual de datos.
--   - UsuarioService.DeleteAsync es soft-delete real (solo marca Actor.IsDeleted),
--     pero tampoco toca EventUsers, asi que un usuario "eliminado" puede seguir
--     apareciendo como participante de eventos.
--   - "Desactivar" (Actor.IsActive = false) es un estado distinto de "eliminar"
--     (Actor.IsDeleted = true) -- ambos se revisan por separado abajo.
--
-- Este script es SOLO LECTURA (SELECT). No borra nada.
-- Ejecutar contra la base de datos de produccion/staging para diagnostico.
-- ============================================================================


-- ----------------------------------------------------------------------------
-- 1) Cumpleanos de STAFF cuyo registro Staff ya NO EXISTE (huerfano real)
--    Caso mas probable del reporte del cliente: staff de prueba eliminado
--    (hard delete) que dejo su evento de cumpleanos colgado en el calendario.
-- ----------------------------------------------------------------------------
SELECT
    e."Id"          AS "EventId",
    e."Title",
    e."StartDate",
    e."CompanyId",
    e."IsGlobal",
    e."StaffId"     AS "StaffId_YaNoExiste"
FROM "Events" e
WHERE e."StaffId" IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM "Staff" s WHERE s."Id" = e."StaffId")
ORDER BY e."CompanyId", e."StartDate";


-- ----------------------------------------------------------------------------
-- 2) Cumpleanos de STAFF que SI existe pero esta INACTIVO o ELIMINADO
--    (Actor.IsActive = false  o  Actor.IsDeleted = true)
-- ----------------------------------------------------------------------------
SELECT
    e."Id"          AS "EventId",
    e."Title",
    e."StartDate",
    e."CompanyId",
    e."IsGlobal",
    st."Id"         AS "StaffId",
    a."FullName",
    a."IsActive"    AS "Actor_IsActive",
    a."IsDeleted"   AS "Actor_IsDeleted",
    a."DeletedAt"   AS "Actor_DeletedAt"
FROM "Events" e
JOIN "Staff" st ON st."Id" = e."StaffId"
JOIN "Actors" a ON a."Id" = st."ActorId"
WHERE e."StaffId" IS NOT NULL
  AND (a."IsActive" = false OR a."IsDeleted" = true)
ORDER BY e."CompanyId", e."StartDate";


-- ----------------------------------------------------------------------------
-- 3) Cumpleanos de CLIENTES cuyo id (extraido del tag "BIRTHDAY:{clientId}"
--    en Events.OtherType) YA NO EXISTE en Clients (huerfano real).
--    En teoria ClientService.DeleteAsync limpia esto, pero se valida por si
--    quedaron registros de antes del fix o por edicion manual de datos.
-- ----------------------------------------------------------------------------
SELECT
    e."Id"                                              AS "EventId",
    e."Title",
    e."StartDate",
    e."CompanyId",
    e."OtherType",
    (regexp_match(e."OtherType", '^BIRTHDAY:(\d+)$'))[1]::int AS "ClientId_YaNoExiste"
FROM "Events" e
WHERE e."OtherType" LIKE 'BIRTHDAY:%'
  AND NOT EXISTS (
      SELECT 1 FROM "Clients" c
      WHERE c."Id" = (regexp_match(e."OtherType", '^BIRTHDAY:(\d+)$'))[1]::int
  )
ORDER BY e."CompanyId", e."StartDate";


-- ----------------------------------------------------------------------------
-- 4) Cumpleanos de CLIENTES que SI existen pero estan INACTIVOS o ELIMINADOS
-- ----------------------------------------------------------------------------
SELECT
    e."Id"          AS "EventId",
    e."Title",
    e."StartDate",
    e."CompanyId",
    c."Id"          AS "ClientId",
    a."FullName",
    a."IsActive"    AS "Actor_IsActive",
    a."IsDeleted"   AS "Actor_IsDeleted",
    a."DeletedAt"   AS "Actor_DeletedAt"
FROM "Events" e
JOIN "Clients" c ON c."Id" = (regexp_match(e."OtherType", '^BIRTHDAY:(\d+)$'))[1]::int
JOIN "Actors" a ON a."Id" = c."ActorId"
WHERE e."OtherType" LIKE 'BIRTHDAY:%'
  AND (a."IsActive" = false OR a."IsDeleted" = true)
ORDER BY e."CompanyId", e."StartDate";


-- ----------------------------------------------------------------------------
-- 5) Participantes de eventos (EventUsers) que apuntan a un USUARIO
--    inactivo o eliminado (soft-deleted). Puede dejar gente "invitada" a
--    eventos que ya no deberia ver el calendario de nadie.
-- ----------------------------------------------------------------------------
SELECT
    eu."Id"         AS "EventUserId",
    eu."EventId",
    e."Title",
    e."StartDate",
    u."Id"          AS "UserId",
    a."FullName",
    a."IsActive"    AS "Actor_IsActive",
    a."IsDeleted"   AS "Actor_IsDeleted",
    a."DeletedAt"   AS "Actor_DeletedAt"
FROM "EventUsers" eu
JOIN "Events" e ON e."Id" = eu."EventId"
LEFT JOIN "Users" u ON u."Id" = eu."UserId"
LEFT JOIN "Actors" a ON a."Id" = u."ActorId"
WHERE u."Id" IS NULL                          -- usuario ya no existe (huerfano)
   OR a."IsActive" = false                    -- usuario desactivado
   OR a."IsDeleted" = true                    -- usuario "eliminado" (soft-delete)
ORDER BY e."CompanyId", e."StartDate";


-- ----------------------------------------------------------------------------
-- 6) Bonus: tipos de evento "Birthday" duplicados por compania.
--    StaffService busca el tipo con Name == 'birthday' (exacto) y ClientService
--    busca con Name ILIKE '%birthday%' (contains) -- con logicas distintas
--    pueden haberse creado mas de un EventType "Birthday" en la practica.
--    (Nota: EventTypes no tiene CompanyId propio en el modelo reportado, se
--    agrupa solo por nombre; ajustar si la tabla real tiene CompanyId.)
-- ----------------------------------------------------------------------------
SELECT
    "Id", "Name", "Color", "Icon", "IsActive", "IsSystem"
FROM "EventTypes"
WHERE lower("Name") LIKE '%birthday%'
ORDER BY "Id";


-- ----------------------------------------------------------------------------
-- 7) Resumen de conteos (para el reporte rapido al cliente)
-- ----------------------------------------------------------------------------
SELECT 'Staff huerfano (Staff ya no existe)' AS "Chequeo", COUNT(*) AS "Cantidad"
FROM "Events" e
WHERE e."StaffId" IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM "Staff" s WHERE s."Id" = e."StaffId")

UNION ALL

SELECT 'Staff inactivo/eliminado (Staff existe)', COUNT(*)
FROM "Events" e
JOIN "Staff" st ON st."Id" = e."StaffId"
JOIN "Actors" a ON a."Id" = st."ActorId"
WHERE e."StaffId" IS NOT NULL
  AND (a."IsActive" = false OR a."IsDeleted" = true)

UNION ALL

SELECT 'Cliente huerfano (Cliente ya no existe)', COUNT(*)
FROM "Events" e
WHERE e."OtherType" LIKE 'BIRTHDAY:%'
  AND NOT EXISTS (
      SELECT 1 FROM "Clients" c
      WHERE c."Id" = (regexp_match(e."OtherType", '^BIRTHDAY:(\d+)$'))[1]::int
  )

UNION ALL

SELECT 'Cliente inactivo/eliminado (Cliente existe)', COUNT(*)
FROM "Events" e
JOIN "Clients" c ON c."Id" = (regexp_match(e."OtherType", '^BIRTHDAY:(\d+)$'))[1]::int
JOIN "Actors" a ON a."Id" = c."ActorId"
WHERE e."OtherType" LIKE 'BIRTHDAY:%'
  AND (a."IsActive" = false OR a."IsDeleted" = true)

UNION ALL

SELECT 'EventUsers apuntando a usuario huerfano/inactivo/eliminado', COUNT(*)
FROM "EventUsers" eu
LEFT JOIN "Users" u ON u."Id" = eu."UserId"
LEFT JOIN "Actors" a ON a."Id" = u."ActorId"
WHERE u."Id" IS NULL
   OR a."IsActive" = false
   OR a."IsDeleted" = true;
