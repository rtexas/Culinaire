-- =============================================================================
-- Culinaire Portal — Vendors & Countries Addendum
-- Run against venus-01 / Culinaire after prior addendums have been applied.
-- =============================================================================

USE [Culinaire];
GO

-- ── Countries ─────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[Countries]'))
BEGIN
    CREATE TABLE [dbo].[Countries]
    (
        [CountryID]   INT           NOT NULL IDENTITY(1,1),
        [Name]        NVARCHAR(100) NOT NULL,
        [Code]        NVARCHAR(10)  NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [IsActive]    BIT           NOT NULL CONSTRAINT [DF_Cty_IsActive]  DEFAULT (1),
        [CreatedAt]   DATETIME      NOT NULL CONSTRAINT [DF_Cty_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_Countries]      PRIMARY KEY CLUSTERED ([CountryID] ASC),
        CONSTRAINT [UQ_Countries_Code] UNIQUE ([Code])
    );
    PRINT 'Table [dbo].[Countries] created.';
END
GO

-- ── Vendors ───────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[Vendors]'))
BEGIN
    CREATE TABLE [dbo].[Vendors]
    (
        [VendorID]                 INT           NOT NULL IDENTITY(1,1),
        [VendorCode]               NVARCHAR(50)  NOT NULL,
        [Name]                     NVARCHAR(200) NOT NULL,
        [Description]              NVARCHAR(500) NULL,
        [Address1]                 NVARCHAR(200) NULL,
        [Address2]                 NVARCHAR(200) NULL,
        [Address3]                 NVARCHAR(200) NULL,
        [City]                     NVARCHAR(100) NULL,
        [StateRegionID]            INT           NULL,
        [PostalCode]               NVARCHAR(20)  NULL,
        [CountryID]                INT           NULL,
        [DefaultPayablesAccountID] INT           NULL,
        [IsActive]                 BIT           NOT NULL CONSTRAINT [DF_Ven_IsActive]  DEFAULT (1),
        [CreatedAt]                DATETIME      NOT NULL CONSTRAINT [DF_Ven_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_Vendors]         PRIMARY KEY CLUSTERED ([VendorID] ASC),
        CONSTRAINT [UQ_Vendors_Code]    UNIQUE ([VendorCode]),
        CONSTRAINT [FK_Ven_State]   FOREIGN KEY ([StateRegionID])            REFERENCES [dbo].[StatesRegions]([StateRegionID]) ON DELETE SET NULL,
        CONSTRAINT [FK_Ven_Country] FOREIGN KEY ([CountryID])                REFERENCES [dbo].[Countries]([CountryID])         ON DELETE SET NULL,
        CONSTRAINT [FK_Ven_CoA]     FOREIGN KEY ([DefaultPayablesAccountID]) REFERENCES [dbo].[ChartOfAccounts]([AccountID])   ON DELETE SET NULL
    );
    PRINT 'Table [dbo].[Vendors] created.';
END
GO

-- ── Verification ──────────────────────────────────────────────────────────────
SELECT 'Countries' AS [Table], COUNT(*) AS [Rows] FROM [dbo].[Countries]
UNION ALL SELECT 'Vendors', COUNT(*) FROM [dbo].[Vendors];
GO
