-- =============================================================
-- Script: 01_create_database.sql
-- Purpose: Create the GarageSpaceStorage database (idempotent)
-- Run against: master
-- =============================================================

USE [master];
GO

IF NOT EXISTS (
    SELECT name
    FROM   sys.databases
    WHERE  name = N'GarageSpaceStorage'
)
BEGIN
    CREATE DATABASE [GarageSpaceStorage];
    PRINT 'Database GarageSpaceStorage created.';
END
ELSE
BEGIN
    PRINT 'Database GarageSpaceStorage already exists – skipped.';
END
GO
