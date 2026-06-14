USE [Culinaire];
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Locations]') AND name = 'Address1')
    ALTER TABLE [dbo].[Locations] ADD [Address1] NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Locations]') AND name = 'Address2')
    ALTER TABLE [dbo].[Locations] ADD [Address2] NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Locations]') AND name = 'City')
    ALTER TABLE [dbo].[Locations] ADD [City] NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Locations]') AND name = 'State')
    ALTER TABLE [dbo].[Locations] ADD [State] NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Locations]') AND name = 'Zip')
    ALTER TABLE [dbo].[Locations] ADD [Zip] NVARCHAR(20) NULL;

SELECT 'Locations address columns added.' AS [Status];
GO
