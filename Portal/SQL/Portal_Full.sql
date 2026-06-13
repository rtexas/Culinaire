-- =============================================================================
-- Culinaire Portal — Complete Database Setup Script
-- Target: SQL Server on venus-01
-- Creates the [Culinaire] database from scratch and builds all tables,
-- constraints, and seed data in a single idempotent run.
-- Safe to re-run: all DDL is guarded with IF NOT EXISTS.
-- =============================================================================

USE [master];
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'Culinaire')
BEGIN
    CREATE DATABASE [Culinaire];
    PRINT 'Database [Culinaire] created.';
END
ELSE
    PRINT 'Database [Culinaire] already exists.';
GO

USE [Culinaire];
GO

-- =============================================================================
-- CORE TABLES
-- =============================================================================

-- ── Settings ──────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[Settings]'))
BEGIN
    CREATE TABLE [dbo].[Settings]
    (
        [SettingID]  INT           NOT NULL IDENTITY(1,1),
        [Name]       NVARCHAR(100) NOT NULL,
        [Value]      NVARCHAR(500) NULL,
        [IsEnabled]  BIT           NOT NULL CONSTRAINT [DF_Settings_IsEnabled] DEFAULT (0),
        CONSTRAINT [PK_Settings] PRIMARY KEY CLUSTERED ([SettingID] ASC)
    );
    PRINT 'Table [dbo].[Settings] created.';
END
GO

-- ── Logging ───────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[Logging]'))
BEGIN
    CREATE TABLE [dbo].[Logging]
    (
        [LogID]    BIGINT        NOT NULL IDENTITY(1,1),
        [LoggedAt] DATETIME      NOT NULL CONSTRAINT [DF_Logging_LoggedAt] DEFAULT (GETDATE()),
        [LogLevel] INT           NOT NULL CONSTRAINT [DF_Logging_LogLevel]  DEFAULT (2),
        [Message]  NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_Logging] PRIMARY KEY CLUSTERED ([LogID] ASC)
    );
    PRINT 'Table [dbo].[Logging] created.';
END
GO

-- ── Users ─────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[Users]'))
BEGIN
    CREATE TABLE [dbo].[Users]
    (
        [UserID]       INT           NOT NULL IDENTITY(1,1),
        [Username]     NVARCHAR(100) NOT NULL,
        [PasswordHash] NVARCHAR(256) NOT NULL,
        [PasswordSalt] NVARCHAR(128) NOT NULL,
        [FullName]     NVARCHAR(200) NOT NULL,
        [Email]        NVARCHAR(200) NULL,
        [RoleType]     NVARCHAR(20)  NOT NULL CONSTRAINT [DF_Users_RoleType]  DEFAULT ('User'),
        [IsActive]     BIT           NOT NULL CONSTRAINT [DF_Users_IsActive]  DEFAULT (1),
        [CreatedAt]    DATETIME      NOT NULL CONSTRAINT [DF_Users_CreatedAt] DEFAULT (GETDATE()),
        [LastLoginAt]  DATETIME      NULL,
        [Address1]     NVARCHAR(200) NULL,
        [Address2]     NVARCHAR(200) NULL,
        [Address3]     NVARCHAR(200) NULL,
        [City]         NVARCHAR(100) NULL,
        [StateRegionID] INT          NULL,
        [PostalCode]   NVARCHAR(20)  NULL,
        [CountryID]    INT           NULL,
        CONSTRAINT [PK_Users]          PRIMARY KEY CLUSTERED ([UserID] ASC),
        CONSTRAINT [UQ_Users_Username] UNIQUE ([Username]),
        CONSTRAINT [CK_Users_RoleType] CHECK ([RoleType] IN ('Administrator','User','Viewer'))
    );
    PRINT 'Table [dbo].[Users] created.';
END
GO

-- ── Modules ───────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[Modules]'))
BEGIN
    CREATE TABLE [dbo].[Modules]
    (
        [ModuleID]    INT           NOT NULL IDENTITY(1,1),
        [ModuleName]  NVARCHAR(100) NOT NULL,
        [DisplayName] NVARCHAR(100) NOT NULL,
        [RouteUrl]    NVARCHAR(200) NOT NULL,
        [IconClass]   NVARCHAR(100) NULL,
        [SortOrder]   INT           NOT NULL CONSTRAINT [DF_Modules_SortOrder] DEFAULT (0),
        [IsActive]    BIT           NOT NULL CONSTRAINT [DF_Modules_IsActive]  DEFAULT (1),
        CONSTRAINT [PK_Modules] PRIMARY KEY CLUSTERED ([ModuleID] ASC)
    );
    PRINT 'Table [dbo].[Modules] created.';
END
GO

-- ── UserModulePermissions ─────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[UserModulePermissions]'))
BEGIN
    CREATE TABLE [dbo].[UserModulePermissions]
    (
        [PermissionID]    INT          NOT NULL IDENTITY(1,1),
        [UserID]          INT          NOT NULL,
        [ModuleID]        INT          NOT NULL,
        [PermissionLevel] NVARCHAR(20) NOT NULL CONSTRAINT [DF_UMP_Level] DEFAULT ('None'),
        CONSTRAINT [PK_UserModulePermissions] PRIMARY KEY CLUSTERED ([PermissionID] ASC),
        CONSTRAINT [FK_UMP_User]   FOREIGN KEY ([UserID])   REFERENCES [dbo].[Users]([UserID])   ON DELETE CASCADE,
        CONSTRAINT [FK_UMP_Module] FOREIGN KEY ([ModuleID]) REFERENCES [dbo].[Modules]([ModuleID]) ON DELETE CASCADE,
        CONSTRAINT [UQ_UMP_UserModule] UNIQUE ([UserID],[ModuleID]),
        CONSTRAINT [CK_UMP_Level] CHECK ([PermissionLevel] IN ('None','Read','ReadWrite'))
    );
    PRINT 'Table [dbo].[UserModulePermissions] created.';
END
GO

-- =============================================================================
-- CHART OF ACCOUNTS TABLES
-- =============================================================================

-- ── AccountCategories ─────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[AccountCategories]'))
BEGIN
    CREATE TABLE [dbo].[AccountCategories]
    (
        [CategoryID]  INT           NOT NULL IDENTITY(1,1),
        [Name]        NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [IsActive]    BIT           NOT NULL CONSTRAINT [DF_AC_IsActive]  DEFAULT (1),
        [CreatedAt]   DATETIME      NOT NULL CONSTRAINT [DF_AC_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_AccountCategories]      PRIMARY KEY CLUSTERED ([CategoryID] ASC),
        CONSTRAINT [UQ_AccountCategories_Name] UNIQUE ([Name])
    );
    PRINT 'Table [dbo].[AccountCategories] created.';
END
GO

-- ── AccountTypes ──────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[AccountTypes]'))
BEGIN
    CREATE TABLE [dbo].[AccountTypes]
    (
        [TypeID]      INT           NOT NULL IDENTITY(1,1),
        [Name]        NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [IsActive]    BIT           NOT NULL CONSTRAINT [DF_AT_IsActive]  DEFAULT (1),
        [CreatedAt]   DATETIME      NOT NULL CONSTRAINT [DF_AT_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_AccountTypes]      PRIMARY KEY CLUSTERED ([TypeID] ASC),
        CONSTRAINT [UQ_AccountTypes_Name] UNIQUE ([Name])
    );
    PRINT 'Table [dbo].[AccountTypes] created.';
END
GO

-- ── ChartOfAccounts ───────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[ChartOfAccounts]'))
BEGIN
    CREATE TABLE [dbo].[ChartOfAccounts]
    (
        [AccountID]          INT           NOT NULL IDENTITY(1,1),
        [AccountName]        NVARCHAR(200) NOT NULL,
        [AccountDescription] NVARCHAR(500) NULL,
        [CategoryID]         INT           NULL,
        [TypeID]             INT           NULL,
        [IsActive]           BIT           NOT NULL CONSTRAINT [DF_CoA_IsActive]  DEFAULT (1),
        [CreatedAt]          DATETIME      NOT NULL CONSTRAINT [DF_CoA_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_ChartOfAccounts]      PRIMARY KEY CLUSTERED ([AccountID] ASC),
        CONSTRAINT [UQ_ChartOfAccounts_Name] UNIQUE ([AccountName]),
        CONSTRAINT [FK_CoA_Category] FOREIGN KEY ([CategoryID]) REFERENCES [dbo].[AccountCategories]([CategoryID]) ON DELETE SET NULL,
        CONSTRAINT [FK_CoA_Type]     FOREIGN KEY ([TypeID])     REFERENCES [dbo].[AccountTypes]([TypeID])          ON DELETE SET NULL
    );
    PRINT 'Table [dbo].[ChartOfAccounts] created.';
END
GO

-- =============================================================================
-- ADDRESS / REFERENCE TABLES
-- =============================================================================

-- ── StatesRegions ─────────────────────────────────────────────────────────────
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

-- ── Users address FKs (added after StatesRegions & Countries exist) ──────────
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Users_State')
    ALTER TABLE [dbo].[Users]
        ADD CONSTRAINT [FK_Users_State] FOREIGN KEY ([StateRegionID])
            REFERENCES [dbo].[StatesRegions]([StateRegionID]) ON DELETE SET NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Users_Country')
    ALTER TABLE [dbo].[Users]
        ADD CONSTRAINT [FK_Users_Country] FOREIGN KEY ([CountryID])
            REFERENCES [dbo].[Countries]([CountryID]) ON DELETE SET NULL;
GO

-- =============================================================================
-- VENDOR TABLES
-- =============================================================================

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
        CONSTRAINT [PK_Vendors]      PRIMARY KEY CLUSTERED ([VendorID] ASC),
        CONSTRAINT [UQ_Vendors_Code] UNIQUE ([VendorCode]),
        CONSTRAINT [FK_Ven_State]    FOREIGN KEY ([StateRegionID])            REFERENCES [dbo].[StatesRegions]([StateRegionID])   ON DELETE SET NULL,
        CONSTRAINT [FK_Ven_Country]  FOREIGN KEY ([CountryID])                REFERENCES [dbo].[Countries]([CountryID])           ON DELETE SET NULL,
        CONSTRAINT [FK_Ven_CoA]      FOREIGN KEY ([DefaultPayablesAccountID]) REFERENCES [dbo].[ChartOfAccounts]([AccountID])     ON DELETE SET NULL
    );
    PRINT 'Table [dbo].[Vendors] created.';
END
GO

-- =============================================================================
-- SEED DATA
-- =============================================================================

-- ── Settings ──────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [Name] = 'Logging.MinLevel')
    INSERT INTO [dbo].[Settings] ([Name],[Value],[IsEnabled]) VALUES ('Logging.MinLevel','Information',1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [Name] = 'Portal.Name')
    INSERT INTO [dbo].[Settings] ([Name],[Value],[IsEnabled]) VALUES ('Portal.Name','Culinaire',1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [Name] = 'Portal.Tagline')
    INSERT INTO [dbo].[Settings] ([Name],[Value],[IsEnabled]) VALUES ('Portal.Tagline','Distinctive Dining & Hospitality Management',1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [Name] = 'Portal.LogoPath')
    INSERT INTO [dbo].[Settings] ([Name],[Value],[IsEnabled]) VALUES ('Portal.LogoPath','/images/logo.png',1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [Name] = 'Theme.BackgroundColor')
    INSERT INTO [dbo].[Settings] ([Name],[Value],[IsEnabled]) VALUES ('Theme.BackgroundColor','#FFFFFF',1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [Name] = 'Theme.TextColor')
    INSERT INTO [dbo].[Settings] ([Name],[Value],[IsEnabled]) VALUES ('Theme.TextColor','#000000',1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [Name] = 'Theme.PrimaryColor')
    INSERT INTO [dbo].[Settings] ([Name],[Value],[IsEnabled]) VALUES ('Theme.PrimaryColor','#2B6B35',1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [Name] = 'Theme.AccentColor')
    INSERT INTO [dbo].[Settings] ([Name],[Value],[IsEnabled]) VALUES ('Theme.AccentColor','#1A4A22',1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [Name] = 'Theme.SidebarBg')
    INSERT INTO [dbo].[Settings] ([Name],[Value],[IsEnabled]) VALUES ('Theme.SidebarBg','#1A4A22',1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [Name] = 'Theme.SidebarText')
    INSERT INTO [dbo].[Settings] ([Name],[Value],[IsEnabled]) VALUES ('Theme.SidebarText','#FFFFFF',1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [Name] = 'Theme.HeaderBg')
    INSERT INTO [dbo].[Settings] ([Name],[Value],[IsEnabled]) VALUES ('Theme.HeaderBg','#2B6B35',1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [Name] = 'Theme.HeaderText')
    INSERT INTO [dbo].[Settings] ([Name],[Value],[IsEnabled]) VALUES ('Theme.HeaderText','#FFFFFF',1);
GO

-- ── Modules ───────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [dbo].[Modules] WHERE [ModuleName] = 'EodSales')
    INSERT INTO [dbo].[Modules] ([ModuleName],[DisplayName],[RouteUrl],[IconClass],[SortOrder],[IsActive])
    VALUES ('EodSales','EOD Sales','/portal/eod-sales','icon-chart',1,1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Modules] WHERE [ModuleName] = 'Payables')
    INSERT INTO [dbo].[Modules] ([ModuleName],[DisplayName],[RouteUrl],[IconClass],[SortOrder],[IsActive])
    VALUES ('Payables','Payables','/portal/payables','icon-payables',2,1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Modules] WHERE [ModuleName] = 'Checks')
    INSERT INTO [dbo].[Modules] ([ModuleName],[DisplayName],[RouteUrl],[IconClass],[SortOrder],[IsActive])
    VALUES ('Checks','Checks','/portal/checks','icon-checks',3,1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Modules] WHERE [ModuleName] = 'Payroll')
    INSERT INTO [dbo].[Modules] ([ModuleName],[DisplayName],[RouteUrl],[IconClass],[SortOrder],[IsActive])
    VALUES ('Payroll','Payroll','/portal/payroll','icon-payroll',4,1);
GO

-- ── Items ─────────────────────────────────────────────────────────────────────
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
GO

-- ── CoaSegments ───────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[CoaSegments]'))
BEGIN
    CREATE TABLE [dbo].[CoaSegments]
    (
        [SegmentNumber] INT           NOT NULL,
        [Description]   NVARCHAR(100) NOT NULL,
        CONSTRAINT [PK_CoaSegments] PRIMARY KEY CLUSTERED ([SegmentNumber] ASC)
    );
    PRINT 'Table [dbo].[CoaSegments] created.';
END
GO

-- ── Locations ─────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[Locations]'))
BEGIN
    CREATE TABLE [dbo].[Locations]
    (
        [LocationID]    INT            NOT NULL IDENTITY(1,1),
        [Code]          NVARCHAR(20)   NOT NULL,
        [Name]          NVARCHAR(100)  NOT NULL,
        [Description]   NVARCHAR(500)  NULL,
        [SegmentNumber] INT            NOT NULL CONSTRAINT [DF_Locations_SegmentNumber] DEFAULT(0),
        [IsActive]      BIT            NOT NULL CONSTRAINT [DF_Locations_IsActive]      DEFAULT (1),
        [CreatedAt]     DATETIME2(0)   NOT NULL CONSTRAINT [DF_Locations_CreatedAt]     DEFAULT (GETDATE()),
        CONSTRAINT [PK_Locations]       PRIMARY KEY CLUSTERED ([LocationID] ASC),
        CONSTRAINT [UQ_Locations_Code]  UNIQUE ([Code])
    );
    PRINT 'Table [dbo].[Locations] created.';
END
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
GO

-- ── ShippingMethods ───────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[ShippingMethods]'))
BEGIN
    CREATE TABLE [dbo].[ShippingMethods]
    (
        [ShippingMethodID] INT           NOT NULL IDENTITY(1,1),
        [Name]             NVARCHAR(100) NOT NULL,
        [Description]      NVARCHAR(500) NULL,
        [IsActive]         BIT           NOT NULL CONSTRAINT [DF_ShipMethods_IsActive] DEFAULT (1),
        CONSTRAINT [PK_ShippingMethods]      PRIMARY KEY CLUSTERED ([ShippingMethodID] ASC),
        CONSTRAINT [UQ_ShippingMethods_Name] UNIQUE ([Name])
    );
    PRINT 'Table [dbo].[ShippingMethods] created.';
END
GO

-- ── PayableHeaders ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[PayableHeaders]'))
BEGIN
    CREATE TABLE [dbo].[PayableHeaders]
    (
        [PayableID]        INT            NOT NULL IDENTITY(1,1),
        [VendorID]         INT            NOT NULL,
        [InvoiceNumber]    NVARCHAR(100)  NOT NULL,
        [InvoiceDate]      DATE           NOT NULL,
        [DueDate]          DATE           NULL,
        [ShippingMethodID] INT            NULL,
        [ShippingCharge]   DECIMAL(18,4)  NOT NULL CONSTRAINT [DF_PayHdr_ShipCharge] DEFAULT (0),
        [TaxAmount]        DECIMAL(18,4)  NOT NULL CONSTRAINT [DF_PayHdr_TaxAmount]  DEFAULT (0),
        [Notes]            NVARCHAR(500)  NULL,
        [Status]           NVARCHAR(20)   NOT NULL CONSTRAINT [DF_PayHdr_Status]     DEFAULT ('Open'),
        [CreatedAt]        DATETIME       NOT NULL CONSTRAINT [DF_PayHdr_CreatedAt]  DEFAULT (GETDATE()),
        [UpdatedAt]        DATETIME       NOT NULL CONSTRAINT [DF_PayHdr_UpdatedAt]  DEFAULT (GETDATE()),
        CONSTRAINT [PK_PayableHeaders] PRIMARY KEY CLUSTERED ([PayableID] ASC),
        CONSTRAINT [FK_PayHdr_Vendor]
            FOREIGN KEY ([VendorID]) REFERENCES [dbo].[Vendors]([VendorID]),
        CONSTRAINT [FK_PayHdr_ShippingMethod]
            FOREIGN KEY ([ShippingMethodID]) REFERENCES [dbo].[ShippingMethods]([ShippingMethodID])
            ON DELETE SET NULL
    );
    PRINT 'Table [dbo].[PayableHeaders] created.';
END
GO

-- ── PayableLineItems ──────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[PayableLineItems]'))
BEGIN
    CREATE TABLE [dbo].[PayableLineItems]
    (
        [LineItemID]    INT            NOT NULL IDENTITY(1,1),
        [PayableID]     INT            NOT NULL,
        [LineNumber]    INT            NOT NULL,
        [ItemID]        INT            NULL,
        [Description]   NVARCHAR(500)  NOT NULL,
        [Quantity]      DECIMAL(18,4)  NOT NULL CONSTRAINT [DF_PayLine_Qty]   DEFAULT (1),
        [UnitPrice]     DECIMAL(18,4)  NOT NULL CONSTRAINT [DF_PayLine_Price] DEFAULT (0),
        [ExtendedPrice] DECIMAL(18,4)  NOT NULL CONSTRAINT [DF_PayLine_Ext]   DEFAULT (0),
        CONSTRAINT [PK_PayableLineItems] PRIMARY KEY CLUSTERED ([LineItemID] ASC),
        CONSTRAINT [FK_PayLine_Payable]
            FOREIGN KEY ([PayableID]) REFERENCES [dbo].[PayableHeaders]([PayableID])
            ON DELETE CASCADE,
        CONSTRAINT [FK_PayLine_Item]
            FOREIGN KEY ([ItemID]) REFERENCES [dbo].[Items]([ItemID])
            ON DELETE SET NULL
    );
    CREATE INDEX [IX_PayableLineItems_PayableID] ON [dbo].[PayableLineItems]([PayableID]);
    PRINT 'Table [dbo].[PayableLineItems] created.';
END
GO

-- =============================================================================
-- VERIFICATION
-- =============================================================================
SELECT t.[name] AS [Table], p.[rows] AS [Rows]
FROM   sys.tables t
JOIN   sys.partitions p ON p.[object_id] = t.[object_id] AND p.[index_id] IN (0,1)
WHERE  t.[type] = 'U'
ORDER  BY t.[name];
GO
