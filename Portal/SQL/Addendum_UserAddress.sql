-- =============================================================================
-- Addendum: Add optional address columns to Users table
-- Run this against an EXISTING Culinaire database.
-- Portal_Full.sql already includes these columns for fresh installs.
-- =============================================================================

USE [Culinaire];
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND name = N'Address1')
BEGIN
    ALTER TABLE [dbo].[Users]
        ADD [Address1]      NVARCHAR(200) NULL,
            [Address2]      NVARCHAR(200) NULL,
            [Address3]      NVARCHAR(200) NULL,
            [City]          NVARCHAR(100) NULL,
            [StateRegionID] INT           NULL,
            [PostalCode]    NVARCHAR(20)  NULL,
            [CountryID]     INT           NULL;
    PRINT 'Address columns added to [dbo].[Users].';
END
ELSE
    PRINT 'Address columns already present on [dbo].[Users].';
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Users_State')
    ALTER TABLE [dbo].[Users]
        ADD CONSTRAINT [FK_Users_State] FOREIGN KEY ([StateRegionID])
            REFERENCES [dbo].[StatesRegions]([StateRegionID]) ON DELETE SET NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Users_Country')
    ALTER TABLE [dbo].[Users]
        ADD CONSTRAINT [FK_Users_Country] FOREIGN KEY ([CountryID])
            REFERENCES [dbo].[Countries]([CountryID]) ON DELETE SET NULL;
GO

PRINT 'Addendum_UserAddress complete.';
GO
