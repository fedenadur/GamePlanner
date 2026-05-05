-- =============================================
-- 01 - Create Database
-- =============================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'GamePlannerDB')
BEGIN
    CREATE DATABASE GamePlannerDB;
END
GO

USE GamePlannerDB;
GO
