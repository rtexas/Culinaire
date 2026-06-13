-- =============================================================================
-- Culinaire Portal — States/Regions Addendum
-- Run against venus-01 / Culinaire after Portal.sql has been applied.
-- =============================================================================

USE [Culinaire];
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[StatesRegions]'))
BEGIN
    CREATE TABLE [dbo].[StatesRegions]
    (
        [StateRegionID] INT           NOT NULL IDENTITY(1,1),
        [Name]          NVARCHAR(100) NOT NULL,
        [Code]          NVARCHAR(20)  NOT NULL,
        [Description]   NVARCHAR(500) NULL,
        [IsActive]      BIT           NOT NULL CONSTRAINT [DF_SR_IsActive]  DEFAULT (1),
        [CreatedAt]     DATETIME      NOT NULL CONSTRAINT [DF_SR_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_StatesRegions]      PRIMARY KEY CLUSTERED ([StateRegionID] ASC),
        CONSTRAINT [UQ_StatesRegions_Code] UNIQUE ([Code])
    );
    PRINT 'Table [dbo].[StatesRegions] created.';
END
GO

SELECT 'StatesRegions' AS [Table], COUNT(*) AS [Rows] FROM [dbo].[StatesRegions];
GO
