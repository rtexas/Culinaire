-- =============================================================================
-- Culinaire Portal — Addendum: Items table
-- Run this script against an existing [Culinaire] database to add the Items
-- feature without recreating the database from scratch.
-- Safe to re-run: all DDL is guarded with IF NOT EXISTS.
-- =============================================================================

USE [Culinaire];
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[Items]'))
BEGIN
    CREATE TABLE [dbo].[Items]
    (
        [ItemID]          INT            NOT NULL IDENTITY(1,1),
        [ItemCode]        NVARCHAR(50)   NOT NULL,
        [ItemName]        NVARCHAR(200)  NOT NULL,
        [ItemDescription] NVARCHAR(500)  NULL,
        [TypicalPrice]    DECIMAL(18,4)  NULL,
        [IsActive]        BIT            NOT NULL CONSTRAINT [DF_Items_IsActive]    DEFAULT (1),
        [CreatedAt]       DATETIME       NOT NULL CONSTRAINT [DF_Items_CreatedAt]   DEFAULT (GETDATE()),
        CONSTRAINT [PK_Items]       PRIMARY KEY CLUSTERED ([ItemID] ASC),
        CONSTRAINT [UQ_Items_Code]  UNIQUE ([ItemCode])
    );
    PRINT 'Table [dbo].[Items] created.';
END
ELSE
    PRINT 'Table [dbo].[Items] already exists — no changes made.';
GO
