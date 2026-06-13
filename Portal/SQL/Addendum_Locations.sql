-- =============================================================================
-- Culinaire Portal — Locations Addendum
-- Run this script against an existing [Culinaire] database that was created
-- before the Locations feature was added.
-- Safe to re-run: all DDL is guarded with IF NOT EXISTS.
-- =============================================================================

USE [Culinaire];
GO

-- ── Locations ─────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[Locations]'))
BEGIN
    CREATE TABLE [dbo].[Locations]
    (
        [LocationID]  INT            NOT NULL IDENTITY(1,1),
        [Code]        NVARCHAR(20)   NOT NULL,
        [Name]        NVARCHAR(100)  NOT NULL,
        [Description] NVARCHAR(500)  NULL,
        [IsActive]    BIT            NOT NULL CONSTRAINT [DF_Locations_IsActive]  DEFAULT (1),
        [CreatedAt]   DATETIME2(0)   NOT NULL CONSTRAINT [DF_Locations_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_Locations]       PRIMARY KEY CLUSTERED ([LocationID] ASC),
        CONSTRAINT [UQ_Locations_Code]  UNIQUE ([Code])
    );
    PRINT 'Table [dbo].[Locations] created.';
END
ELSE
    PRINT 'Table [dbo].[Locations] already exists.';
GO

-- ── UserLocations ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[UserLocations]'))
BEGIN
    CREATE TABLE [dbo].[UserLocations]
    (
        [UserLocationID] INT          NOT NULL IDENTITY(1,1),
        [UserID]         INT          NOT NULL,
        [LocationID]     INT          NOT NULL,
        [CreatedAt]      DATETIME2(0) NOT NULL CONSTRAINT [DF_UserLocations_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_UserLocations]   PRIMARY KEY CLUSTERED ([UserLocationID] ASC),
        CONSTRAINT [UQ_UserLocations]   UNIQUE ([UserID],[LocationID]),
        CONSTRAINT [FK_UserLoc_User]
            FOREIGN KEY ([UserID])     REFERENCES [dbo].[Users]([UserID])     ON DELETE CASCADE,
        CONSTRAINT [FK_UserLoc_Location]
            FOREIGN KEY ([LocationID]) REFERENCES [dbo].[Locations]([LocationID]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_UserLocations_UserID]     ON [dbo].[UserLocations]([UserID]);
    CREATE INDEX [IX_UserLocations_LocationID] ON [dbo].[UserLocations]([LocationID]);
    PRINT 'Table [dbo].[UserLocations] created.';
END
ELSE
    PRINT 'Table [dbo].[UserLocations] already exists.';
GO

SELECT 'Locations addendum complete.' AS [Status];
GO
