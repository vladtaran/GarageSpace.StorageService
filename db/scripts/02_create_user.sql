-- =============================================================
-- Script: 02_create_user.sql
-- Purpose: Create SQL login GarageSpaceStorageUser, map it to
--          the GarageSpaceStorage database, and grant the
--          permissions needed by the application and EF Core
--          migrations (read, write, DDL).
--
-- IMPORTANT: Replace the placeholder password below with your
--            own strong password BEFORE running this script.
--            Use the SAME password when configuring User Secrets
--            (see db/scripts/README.md).
-- =============================================================

USE [master];
GO

-- ── 1. Create SQL Server login ────────────────────────────────
-- Replace <YourStrongPassword!1> with your own password.
IF NOT EXISTS (
    SELECT name
    FROM   sys.server_principals
    WHERE  name = N'GarageSpaceStorageUser'
)
BEGIN
    CREATE LOGIN [GarageSpaceStorageUser]
        WITH PASSWORD     = N'<YourStrongPassword!1>',
             CHECK_POLICY = ON,
             CHECK_EXPIRATION = OFF;
    PRINT 'Login GarageSpaceStorageUser created.';
END
ELSE
BEGIN
    PRINT 'Login GarageSpaceStorageUser already exists – skipped.';
END
GO

-- ── 2. Create database user mapped to the login ───────────────
USE [GarageSpaceStorage];
GO

IF NOT EXISTS (
    SELECT name
    FROM   sys.database_principals
    WHERE  name = N'GarageSpaceStorageUser'
)
BEGIN
    CREATE USER [GarageSpaceStorageUser]
        FOR LOGIN [GarageSpaceStorageUser];
    PRINT 'Database user GarageSpaceStorageUser created.';
END
ELSE
BEGIN
    PRINT 'Database user GarageSpaceStorageUser already exists – skipped.';
END
GO

-- ── 3. Grant application permissions ─────────────────────────
-- db_datareader  : SELECT on all tables
-- db_datawriter  : INSERT / UPDATE / DELETE on all tables
-- db_ddladmin    : CREATE / ALTER / DROP tables & indexes
--                  (required for EF Core migrations)
ALTER ROLE [db_datareader] ADD MEMBER [GarageSpaceStorageUser];
ALTER ROLE [db_datawriter] ADD MEMBER [GarageSpaceStorageUser];
ALTER ROLE [db_ddladmin]   ADD MEMBER [GarageSpaceStorageUser];
PRINT 'Roles granted to GarageSpaceStorageUser.';
GO
