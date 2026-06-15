-- =============================================================================
-- Culinaire Portal -- Complete Database Setup Script
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

-- Settings
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

-- Logging
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

-- Users
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[Users]'))
BEGIN
    CREATE TABLE [dbo].[Users]
    (
        [UserID]        INT           NOT NULL IDENTITY(1,1),
        [Username]      NVARCHAR(100) NOT NULL,
        [PasswordHash]  NVARCHAR(256) NOT NULL,
        [PasswordSalt]  NVARCHAR(128) NOT NULL,
        [FullName]      NVARCHAR(200) NOT NULL,
        [Email]         NVARCHAR(200) NULL,
        [RoleType]      NVARCHAR(20)  NOT NULL CONSTRAINT [DF_Users_RoleType]  DEFAULT ('User'),
        [IsActive]      BIT           NOT NULL CONSTRAINT [DF_Users_IsActive]  DEFAULT (1),
        [CreatedAt]     DATETIME      NOT NULL CONSTRAINT [DF_Users_CreatedAt] DEFAULT (GETDATE()),
        [LastLoginAt]   DATETIME      NULL,
        [Address1]      NVARCHAR(200) NULL,
        [Address2]      NVARCHAR(200) NULL,
        [Address3]      NVARCHAR(200) NULL,
        [City]          NVARCHAR(100) NULL,
        [StateRegionID] INT           NULL,
        [PostalCode]    NVARCHAR(20)  NULL,
        [CountryID]     INT           NULL,
        CONSTRAINT [PK_Users]          PRIMARY KEY CLUSTERED ([UserID] ASC),
        CONSTRAINT [UQ_Users_Username] UNIQUE ([Username]),
        CONSTRAINT [CK_Users_RoleType] CHECK ([RoleType] IN ('Administrator','User','Viewer'))
    );
    PRINT 'Table [dbo].[Users] created.';
END
GO

-- Modules
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

-- UserModulePermissions
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[UserModulePermissions]'))
BEGIN
    CREATE TABLE [dbo].[UserModulePermissions]
    (
        [PermissionID]    INT          NOT NULL IDENTITY(1,1),
        [UserID]          INT          NOT NULL,
        [ModuleID]        INT          NOT NULL,
        [PermissionLevel] NVARCHAR(20) NOT NULL CONSTRAINT [DF_UMP_Level] DEFAULT ('None'),
        CONSTRAINT [PK_UserModulePermissions] PRIMARY KEY CLUSTERED ([PermissionID] ASC),
        CONSTRAINT [FK_UMP_User]       FOREIGN KEY ([UserID])   REFERENCES [dbo].[Users]([UserID])     ON DELETE CASCADE,
        CONSTRAINT [FK_UMP_Module]     FOREIGN KEY ([ModuleID]) REFERENCES [dbo].[Modules]([ModuleID]) ON DELETE CASCADE,
        CONSTRAINT [UQ_UMP_UserModule] UNIQUE ([UserID],[ModuleID]),
        CONSTRAINT [CK_UMP_Level]      CHECK ([PermissionLevel] IN ('None','Read','ReadWrite','EditAndVoid'))
    );
    PRINT 'Table [dbo].[UserModulePermissions] created.';
END
GO

-- =============================================================================
-- CHART OF ACCOUNTS TABLES
-- =============================================================================

-- AccountCategories
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

-- AccountTypes
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

-- ChartOfAccounts
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
        [FullAccountString]  NVARCHAR(200) NULL,
        [Seg1Value]          NVARCHAR(50)  NULL,
        [Seg2Value]          NVARCHAR(50)  NULL,
        [Seg3Value]          NVARCHAR(50)  NULL,
        [Seg4Value]          NVARCHAR(50)  NULL,
        [Seg5Value]          NVARCHAR(50)  NULL,
        [Seg6Value]          NVARCHAR(50)  NULL,
        [CreatedAt]          DATETIME2(0)  NOT NULL CONSTRAINT [DF_CoA_CreatedAt] DEFAULT (GETDATE()),
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

-- StatesRegions
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

-- Countries
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

-- Users address FKs (added after StatesRegions & Countries exist)
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

-- Vendors
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

-- Settings
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

IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [Name] = 'Theme.FooterBg')
    INSERT INTO [dbo].[Settings] ([Name],[Value],[IsEnabled]) VALUES ('Theme.FooterBg','#1A4A22',1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [Name] = 'Theme.FooterText')
    INSERT INTO [dbo].[Settings] ([Name],[Value],[IsEnabled]) VALUES ('Theme.FooterText','#FFFFFF',1);

IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [Name] = 'CoaAccountDelimiter')
    INSERT INTO [dbo].[Settings] ([Name],[Value],[IsEnabled]) VALUES ('CoaAccountDelimiter','-',1);
GO

-- Modules
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

-- PayableTerms seed
MERGE [dbo].[PayableTerms] AS tgt
USING (VALUES
    ('Net 10',        'Due 10 days after invoice date',                 'NET10',  10, 'Invoice'),
    ('Net 15',        'Due 15 days after invoice date',                 'NET15',  15, 'Invoice'),
    ('Net 30',        'Due 30 days after invoice date',                 'NET30',  30, 'Invoice'),
    ('Net 45',        'Due 45 days after invoice date',                 'NET45',  45, 'Invoice'),
    ('Net 60',        'Due 60 days after invoice date',                 'NET60',  60, 'Invoice'),
    ('Net 90',        'Due 90 days after invoice date',                 'NET90',  90, 'Invoice'),
    ('Due on Receipt','Due immediately upon receipt',                   'DOR',     0, 'Invoice'),
    ('1st of Month',  'Due the 1st of the following month',             'DOM01',   1, 'FixedDOM'),
    ('15th of Month', 'Due the 15th of the current/next month',         'DOM15',  15, 'FixedDOM'),
    ('EOM',           'Due end of current month (approx 30 days today)','EOM',    30, 'Current')
) AS src ([Name],[Description],[ExternalCode],[NumberOfDays],[DateBasis])
ON tgt.[Name] = src.[Name]
WHEN NOT MATCHED THEN
    INSERT ([Name],[Description],[ExternalCode],[NumberOfDays],[DateBasis])
    VALUES (src.[Name],src.[Description],src.[ExternalCode],src.[NumberOfDays],src.[DateBasis]);
GO

-- =============================================================================
-- ITEMS TABLE
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[Items]'))
BEGIN
    CREATE TABLE [dbo].[Items]
    (
        [ItemID]          INT            NOT NULL IDENTITY(1,1),
        [ItemCode]        NVARCHAR(50)   NOT NULL,
        [ItemName]        NVARCHAR(200)  NOT NULL,
        [ItemDescription] NVARCHAR(500)  NULL,
        [TypicalPrice]    DECIMAL(18,4)  NULL,
        [IsActive]        BIT            NOT NULL CONSTRAINT [DF_Items_IsActive]  DEFAULT (1),
        [CreatedAt]       DATETIME       NOT NULL CONSTRAINT [DF_Items_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_Items]      PRIMARY KEY CLUSTERED ([ItemID] ASC),
        CONSTRAINT [UQ_Items_Code] UNIQUE ([ItemCode])
    );
    PRINT 'Table [dbo].[Items] created.';
END
GO

-- =============================================================================
-- COA SEGMENTS TABLE
-- =============================================================================

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

-- CoaSegments seed -- COA structure: Dept (1) - Location (2) - Natural Account (3)
IF NOT EXISTS (SELECT 1 FROM [dbo].[CoaSegments] WHERE [SegmentNumber] = 1)
    INSERT INTO [dbo].[CoaSegments] ([SegmentNumber],[Description]) VALUES (1, 'Department');
ELSE
    UPDATE [dbo].[CoaSegments] SET [Description] = 'Department' WHERE [SegmentNumber] = 1;

IF NOT EXISTS (SELECT 1 FROM [dbo].[CoaSegments] WHERE [SegmentNumber] = 2)
    INSERT INTO [dbo].[CoaSegments] ([SegmentNumber],[Description]) VALUES (2, 'Location');
ELSE
    UPDATE [dbo].[CoaSegments] SET [Description] = 'Location' WHERE [SegmentNumber] = 2;

IF NOT EXISTS (SELECT 1 FROM [dbo].[CoaSegments] WHERE [SegmentNumber] = 3)
    INSERT INTO [dbo].[CoaSegments] ([SegmentNumber],[Description]) VALUES (3, 'Natural Account');
ELSE
    UPDATE [dbo].[CoaSegments] SET [Description] = 'Natural Account' WHERE [SegmentNumber] = 3;
GO

-- =============================================================================
-- LOCATION TABLES
-- =============================================================================

-- Locations
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[Locations]'))
BEGIN
    CREATE TABLE [dbo].[Locations]
    (
        [LocationID]    INT            NOT NULL IDENTITY(1,1),
        [Code]          NVARCHAR(20)   NOT NULL,
        [Name]          NVARCHAR(100)  NOT NULL,
        [Description]   NVARCHAR(500)  NULL,
        [SegmentNumber] INT            NOT NULL CONSTRAINT [DF_Locations_SegmentNumber] DEFAULT (0),
        [Address1]      NVARCHAR(100)  NULL,
        [Address2]      NVARCHAR(100)  NULL,
        [City]          NVARCHAR(100)  NULL,
        [State]         NVARCHAR(50)   NULL,
        [Zip]           NVARCHAR(20)   NULL,
        [IsActive]      BIT            NOT NULL CONSTRAINT [DF_Locations_IsActive]      DEFAULT (1),
        [CreatedAt]     DATETIME2(0)   NOT NULL CONSTRAINT [DF_Locations_CreatedAt]     DEFAULT (GETDATE()),
        CONSTRAINT [PK_Locations]      PRIMARY KEY CLUSTERED ([LocationID] ASC),
        CONSTRAINT [UQ_Locations_Code] UNIQUE ([Code])
    );
    PRINT 'Table [dbo].[Locations] created.';
END
GO

-- UserLocations
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[UserLocations]'))
BEGIN
    CREATE TABLE [dbo].[UserLocations]
    (
        [UserLocationID] INT          NOT NULL IDENTITY(1,1),
        [UserID]         INT          NOT NULL,
        [LocationID]     INT          NOT NULL,
        [CreatedAt]      DATETIME2(0) NOT NULL CONSTRAINT [DF_UserLocations_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_UserLocations]    PRIMARY KEY CLUSTERED ([UserLocationID] ASC),
        CONSTRAINT [UQ_UserLocations]    UNIQUE ([UserID],[LocationID]),
        CONSTRAINT [FK_UserLoc_User]     FOREIGN KEY ([UserID])     REFERENCES [dbo].[Users]([UserID])         ON DELETE CASCADE,
        CONSTRAINT [FK_UserLoc_Location] FOREIGN KEY ([LocationID]) REFERENCES [dbo].[Locations]([LocationID]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_UserLocations_UserID]     ON [dbo].[UserLocations]([UserID]);
    CREATE INDEX [IX_UserLocations_LocationID] ON [dbo].[UserLocations]([LocationID]);
    PRINT 'Table [dbo].[UserLocations] created.';
END
GO

-- =============================================================================
-- EOD SALES TABLES
-- =============================================================================

-- EodSections
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[EodSections]'))
BEGIN
    CREATE TABLE [dbo].[EodSections] (
        [SectionID]     INT           NOT NULL IDENTITY(1,1),
        [Name]          NVARCHAR(200) NOT NULL,
        [Description]   NVARCHAR(500) NULL,
        [Multiplier]    INT           NOT NULL CONSTRAINT [DF_EodSections_Multiplier]    DEFAULT (1),
        [UseInEodSales] BIT           NOT NULL CONSTRAINT [DF_EodSections_UseInEodSales] DEFAULT (1),
        [UseInEodGraph] BIT           NOT NULL CONSTRAINT [DF_EodSections_UseInEodGraph] DEFAULT (0),
        [CreatedAt]     DATETIME2(0)  NOT NULL CONSTRAINT [DF_EodSections_CreatedAt]     DEFAULT (GETDATE()),
        CONSTRAINT [PK_EodSections]      PRIMARY KEY CLUSTERED ([SectionID] ASC),
        CONSTRAINT [UQ_EodSections_Name] UNIQUE ([Name]),
        CONSTRAINT [CK_EodSections_Mult] CHECK ([Multiplier] IN (-1, 0, 1))
    );
    PRINT 'Table [dbo].[EodSections] created.';
END
ELSE
BEGIN
    -- Add UseInEodSales column to existing installations
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[EodSections]') AND name = N'UseInEodSales')
    BEGIN
        ALTER TABLE [dbo].[EodSections]
            ADD [UseInEodSales] BIT NOT NULL CONSTRAINT [DF_EodSections_UseInEodSales] DEFAULT (1);
        PRINT 'Column [UseInEodSales] added to [dbo].[EodSections].';
    END
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[EodSections]') AND name = N'UseInEodGraph')
    BEGIN
        ALTER TABLE [dbo].[EodSections]
            ADD [UseInEodGraph] BIT NOT NULL CONSTRAINT [DF_EodSections_UseInEodGraph] DEFAULT (0);
        PRINT 'Column [UseInEodGraph] added to [dbo].[EodSections].';
    END
END
GO

-- EodRows
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[EodRows]'))
BEGIN
    CREATE TABLE [dbo].[EodRows] (
        [RowID]       INT           NOT NULL IDENTITY(1,1),
        [Name]        NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [SectionID]   INT           NOT NULL CONSTRAINT [DF_EodRows_SectionID] DEFAULT (0),
        [CreatedAt]   DATETIME2(0)  NOT NULL CONSTRAINT [DF_EodRows_CreatedAt]  DEFAULT (GETDATE()),
        CONSTRAINT [PK_EodRows]      PRIMARY KEY CLUSTERED ([RowID] ASC),
        CONSTRAINT [UQ_EodRows_Name] UNIQUE ([Name])
    );
    PRINT 'Table [dbo].[EodRows] created.';
END
GO

-- EodColumns
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[EodColumns]'))
BEGIN
    CREATE TABLE [dbo].[EodColumns] (
        [ColumnID]         INT           NOT NULL IDENTITY(1,1),
        [Name]             NVARCHAR(200) NOT NULL,
        [Description]      NVARCHAR(500) NULL,
        [CoaSegmentNumber] INT           NOT NULL CONSTRAINT [DF_EodColumns_CoaSeg]    DEFAULT (0),
        [SegmentValue]     NVARCHAR(50)  NULL,
        [CreatedAt]        DATETIME2(0)  NOT NULL CONSTRAINT [DF_EodColumns_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_EodColumns]      PRIMARY KEY CLUSTERED ([ColumnID] ASC),
        CONSTRAINT [UQ_EodColumns_Name] UNIQUE ([Name])
    );
    PRINT 'Table [dbo].[EodColumns] created.';
END
GO

-- EodSetups
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[EodSetups]'))
BEGIN
    CREATE TABLE [dbo].[EodSetups] (
        [SetupID]       INT           NOT NULL IDENTITY(1,1),
        [LocationID]    INT           NOT NULL,
        [VersionNumber] INT           NOT NULL,
        [Description]   NVARCHAR(200) NULL,
        [IsEnabled]     BIT           NOT NULL CONSTRAINT [DF_EodSetups_IsEnabled] DEFAULT (0),
        [CreatedAt]     DATETIME2(0)  NOT NULL CONSTRAINT [DF_EodSetups_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_EodSetups]        PRIMARY KEY CLUSTERED ([SetupID] ASC),
        CONSTRAINT [UQ_EodSetups_LocVer] UNIQUE ([LocationID],[VersionNumber])
    );
    PRINT 'Table [dbo].[EodSetups] created.';
END
GO

-- EodSetupColumns
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[EodSetupColumns]'))
BEGIN
    CREATE TABLE [dbo].[EodSetupColumns] (
        [SetupColumnID] INT NOT NULL IDENTITY(1,1),
        [SetupID]       INT NOT NULL,
        [ColumnID]      INT NOT NULL,
        [DisplayOrder]  INT NOT NULL CONSTRAINT [DF_EodSetupCols_Order] DEFAULT (0),
        CONSTRAINT [PK_EodSetupColumns] PRIMARY KEY CLUSTERED ([SetupColumnID] ASC),
        CONSTRAINT [UQ_EodSetupColumns] UNIQUE ([SetupID],[ColumnID])
    );
    PRINT 'Table [dbo].[EodSetupColumns] created.';
END
GO

-- EodSetupRows
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[EodSetupRows]'))
BEGIN
    CREATE TABLE [dbo].[EodSetupRows] (
        [SetupRowID]   INT NOT NULL IDENTITY(1,1),
        [SetupID]      INT NOT NULL,
        [RowID]        INT NOT NULL,
        [DisplayOrder] INT NOT NULL CONSTRAINT [DF_EodSetupRows_Order] DEFAULT (0),
        CONSTRAINT [PK_EodSetupRows] PRIMARY KEY CLUSTERED ([SetupRowID] ASC),
        CONSTRAINT [UQ_EodSetupRows] UNIQUE ([SetupID],[RowID])
    );
    PRINT 'Table [dbo].[EodSetupRows] created.';
END
GO

-- EodSetupCells
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[EodSetupCells]'))
BEGIN
    CREATE TABLE [dbo].[EodSetupCells] (
        [CellID]    INT NOT NULL IDENTITY(1,1),
        [SetupID]   INT NOT NULL,
        [RowID]     INT NOT NULL,
        [ColumnID]  INT NOT NULL,
        [AccountID] INT NULL,
        CONSTRAINT [PK_EodSetupCells] PRIMARY KEY CLUSTERED ([CellID] ASC),
        CONSTRAINT [UQ_EodSetupCells] UNIQUE ([SetupID],[RowID],[ColumnID])
    );
    PRINT 'Table [dbo].[EodSetupCells] created.';
END
GO

-- EodSalesEntries
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[EodSalesEntries]'))
BEGIN
    CREATE TABLE [dbo].[EodSalesEntries] (
        [EntryID]     INT          NOT NULL IDENTITY(1,1),
        [EntryDate]   DATE         NOT NULL,
        [LocationID]  INT          NOT NULL,
        [SetupID]     INT          NOT NULL,
        [IsSubmitted] BIT          NOT NULL CONSTRAINT [DF_EodEntries_Sub]       DEFAULT (0),
        [SubmittedAt] DATETIME2(0) NULL,
        [IsVoided]    BIT          NOT NULL CONSTRAINT [DF_EodEntries_IsVoided]  DEFAULT (0),
        [VoidedAt]    DATETIME2(0) NULL,
        [CreatedAt]   DATETIME2(0) NOT NULL CONSTRAINT [DF_EodEntries_CreatedAt] DEFAULT (GETDATE()),
        [UpdatedAt]   DATETIME2(0) NOT NULL CONSTRAINT [DF_EodEntries_UpdatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_EodSalesEntries] PRIMARY KEY CLUSTERED ([EntryID] ASC),
        CONSTRAINT [UQ_EodSalesEntries] UNIQUE ([EntryDate],[LocationID],[SetupID])
    );
    PRINT 'Table [dbo].[EodSalesEntries] created.';
END
GO

-- EodSalesValues
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[EodSalesValues]'))
BEGIN
    CREATE TABLE [dbo].[EodSalesValues] (
        [ValueID]  INT           NOT NULL IDENTITY(1,1),
        [EntryID]  INT           NOT NULL,
        [RowID]    INT           NOT NULL,
        [ColumnID] INT           NOT NULL,
        [Amount]   DECIMAL(18,2) NOT NULL CONSTRAINT [DF_EodSalesValues_Amt] DEFAULT (0),
        CONSTRAINT [PK_EodSalesValues] PRIMARY KEY CLUSTERED ([ValueID] ASC),
        CONSTRAINT [UQ_EodSalesValues] UNIQUE ([EntryID],[RowID],[ColumnID])
    );
    PRINT 'Table [dbo].[EodSalesValues] created.';
END
GO

-- =============================================================================
-- PAYABLES TABLES
-- =============================================================================

-- ShippingMethods
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

-- PayableTerms
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[PayableTerms]'))
BEGIN
    CREATE TABLE [dbo].[PayableTerms]
    (
        [PayableTermID] INT           NOT NULL IDENTITY(1,1),
        [Name]          NVARCHAR(100) NOT NULL,
        [Description]   NVARCHAR(500) NULL,
        [ExternalCode]  NVARCHAR(50)  NULL,
        [NumberOfDays]  INT           NULL,
        -- "Invoice"  = days after invoice date
        -- "Current"  = days after current date
        -- "FixedDOM" = fixed day of month (NumberOfDays = day number 1–31)
        [DateBasis]     NVARCHAR(20)  NOT NULL CONSTRAINT [DF_PayTerms_DateBasis] DEFAULT ('Invoice'),
        [CreatedAt]     DATETIME      NOT NULL CONSTRAINT [DF_PayTerms_CreatedAt] DEFAULT (GETDATE()),
        [UpdatedAt]     DATETIME      NOT NULL CONSTRAINT [DF_PayTerms_UpdatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_PayableTerms]      PRIMARY KEY CLUSTERED ([PayableTermID] ASC),
        CONSTRAINT [UQ_PayableTerms_Name] UNIQUE ([Name]),
        CONSTRAINT [CK_PayableTerms_Basis] CHECK ([DateBasis] IN ('Invoice','Current','FixedDOM'))
    );
    PRINT 'Table [dbo].[PayableTerms] created.';
END
GO

-- PayableHeaders
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[PayableHeaders]'))
BEGIN
    CREATE TABLE [dbo].[PayableHeaders]
    (
        [PayableID]        INT            NOT NULL IDENTITY(1,1),
        [VendorID]         INT            NOT NULL,
        [InvoiceNumber]    NVARCHAR(100)  NOT NULL,
        [InvoiceDate]      DATE           NOT NULL,
        [DueDate]          DATE           NULL,
        [DueDateTermID]    INT            NULL,
        [ShippingMethodID] INT            NULL,
        [ShippingCharge]   DECIMAL(18,4)  NOT NULL CONSTRAINT [DF_PayHdr_ShipCharge] DEFAULT (0),
        [TaxAmount]        DECIMAL(18,4)  NOT NULL CONSTRAINT [DF_PayHdr_TaxAmount]  DEFAULT (0),
        [Notes]            NVARCHAR(500)  NULL,
        [LocationID]       INT            NULL,
        [Status]           NVARCHAR(20)   NOT NULL CONSTRAINT [DF_PayHdr_Status]     DEFAULT ('Saved'),
        [CreatedAt]        DATETIME       NOT NULL CONSTRAINT [DF_PayHdr_CreatedAt]  DEFAULT (GETDATE()),
        [UpdatedAt]        DATETIME       NOT NULL CONSTRAINT [DF_PayHdr_UpdatedAt]  DEFAULT (GETDATE()),
        CONSTRAINT [PK_PayableHeaders]        PRIMARY KEY CLUSTERED ([PayableID] ASC),
        CONSTRAINT [FK_PayHdr_Location]       FOREIGN KEY ([LocationID])    REFERENCES [dbo].[Locations]([LocationID]),
        CONSTRAINT [FK_PayHdr_Vendor]         FOREIGN KEY ([VendorID])      REFERENCES [dbo].[Vendors]([VendorID]),
        CONSTRAINT [FK_PayHdr_ShippingMethod] FOREIGN KEY ([ShippingMethodID]) REFERENCES [dbo].[ShippingMethods]([ShippingMethodID]) ON DELETE SET NULL,
        CONSTRAINT [FK_PayHdr_PayableTerm]    FOREIGN KEY ([DueDateTermID]) REFERENCES [dbo].[PayableTerms]([PayableTermID]) ON DELETE SET NULL
    );
    PRINT 'Table [dbo].[PayableHeaders] created.';
END
ELSE
BEGIN
    -- Add DueDateTermID to existing installations
    IF NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'[dbo].[PayableHeaders]') AND name = N'DueDateTermID')
    BEGIN
        ALTER TABLE [dbo].[PayableHeaders]
            ADD [DueDateTermID] INT NULL
                CONSTRAINT [FK_PayHdr_PayableTerm]
                REFERENCES [dbo].[PayableTerms]([PayableTermID]) ON DELETE SET NULL;
        PRINT 'Column [DueDateTermID] added to [dbo].[PayableHeaders].';
    END
END
GO

-- PayableLineItems
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
        CONSTRAINT [FK_PayLine_Payable]  FOREIGN KEY ([PayableID]) REFERENCES [dbo].[PayableHeaders]([PayableID]) ON DELETE CASCADE,
        CONSTRAINT [FK_PayLine_Item]     FOREIGN KEY ([ItemID])    REFERENCES [dbo].[Items]([ItemID])             ON DELETE SET NULL
    );
    CREATE INDEX [IX_PayableLineItems_PayableID] ON [dbo].[PayableLineItems]([PayableID]);
    PRINT 'Table [dbo].[PayableLineItems] created.';
END
GO

-- =============================================================================
-- CHECK TABLES
-- =============================================================================

-- CheckSetupVendors
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[CheckSetupVendors]'))
BEGIN
    CREATE TABLE [dbo].[CheckSetupVendors] (
        [CheckSetupVendorID] INT NOT NULL IDENTITY(1,1),
        [LocationID]         INT NOT NULL,
        [VendorID]           INT NOT NULL,
        [IsActive]           BIT NOT NULL CONSTRAINT [DF_CSV_Active] DEFAULT (1),
        CONSTRAINT [PK_CheckSetupVendors] PRIMARY KEY ([CheckSetupVendorID]),
        CONSTRAINT [UQ_CheckSetupVendors] UNIQUE ([LocationID],[VendorID]),
        CONSTRAINT [FK_CSV_Location] FOREIGN KEY ([LocationID]) REFERENCES [dbo].[Locations]([LocationID]) ON DELETE CASCADE,
        CONSTRAINT [FK_CSV_Vendor]   FOREIGN KEY ([VendorID])   REFERENCES [dbo].[Vendors]([VendorID])    ON DELETE CASCADE
    );
    PRINT 'Table [dbo].[CheckSetupVendors] created.';
END
GO

-- CheckSetupAccounts
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[CheckSetupAccounts]'))
BEGIN
    CREATE TABLE [dbo].[CheckSetupAccounts] (
        [CheckSetupAccountID] INT NOT NULL IDENTITY(1,1),
        [LocationID]          INT NOT NULL,
        [AccountID]           INT NOT NULL,
        [IsActive]            BIT NOT NULL CONSTRAINT [DF_CSA_Active] DEFAULT (1),
        CONSTRAINT [PK_CheckSetupAccounts] PRIMARY KEY ([CheckSetupAccountID]),
        CONSTRAINT [UQ_CheckSetupAccounts] UNIQUE ([LocationID],[AccountID]),
        CONSTRAINT [FK_CSA_Location] FOREIGN KEY ([LocationID]) REFERENCES [dbo].[Locations]([LocationID])     ON DELETE CASCADE,
        CONSTRAINT [FK_CSA_Account]  FOREIGN KEY ([AccountID])  REFERENCES [dbo].[ChartOfAccounts]([AccountID]) ON DELETE CASCADE
    );
    PRINT 'Table [dbo].[CheckSetupAccounts] created.';
END
GO

-- CheckTransactions
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[CheckTransactions]'))
BEGIN
    CREATE TABLE [dbo].[CheckTransactions] (
        [CheckTransactionID]   INT            NOT NULL IDENTITY(1,1),
        [LocationID]           INT            NOT NULL,
        [CheckNumber]          INT            NOT NULL CONSTRAINT [DF_CT_CheckNum]   DEFAULT (0),
        [TransactionDate]      DATE           NOT NULL,
        [VendorID]             INT            NULL,
        [IsManualVendor]       BIT            NOT NULL CONSTRAINT [DF_CT_Manual]     DEFAULT (0),
        [ManualVendorName]     NVARCHAR(100)  NULL,
        [ManualVendorAddress1] NVARCHAR(100)  NULL,
        [ManualVendorAddress2] NVARCHAR(100)  NULL,
        [ManualVendorCity]     NVARCHAR(100)  NULL,
        [ManualVendorState]    NVARCHAR(50)   NULL,
        [ManualVendorZip]      NVARCHAR(20)   NULL,
        [Amount]               DECIMAL(18,2)  NOT NULL,
        [Memo]                 NVARCHAR(500)  NULL,
        [ExpenseAccountID]     INT            NULL,
        [IsSubmitted]          BIT            NOT NULL CONSTRAINT [DF_CT_Submitted]  DEFAULT (0),
        [SubmittedAt]          DATETIME2(0)   NULL,
        [SubmittedByUserID]    INT            NULL,
        [IsVoided]             BIT            NOT NULL CONSTRAINT [DF_CT_Voided]     DEFAULT (0),
        [VoidedAt]             DATETIME2(0)   NULL,
        [VoidedByUserID]       INT            NULL,
        [CreatedByUserID]      INT            NULL,
        [CreatedAt]            DATETIME2(0)   NOT NULL CONSTRAINT [DF_CT_Created]    DEFAULT (GETDATE()),
        CONSTRAINT [PK_CheckTransactions] PRIMARY KEY ([CheckTransactionID]),
        CONSTRAINT [FK_CT_Location] FOREIGN KEY ([LocationID])       REFERENCES [dbo].[Locations]([LocationID]),
        CONSTRAINT [FK_CT_Vendor]   FOREIGN KEY ([VendorID])         REFERENCES [dbo].[Vendors]([VendorID]),
        CONSTRAINT [FK_CT_Account]  FOREIGN KEY ([ExpenseAccountID]) REFERENCES [dbo].[ChartOfAccounts]([AccountID])
    );
    CREATE INDEX [IX_CT_Location] ON [dbo].[CheckTransactions]([LocationID],[TransactionDate] DESC);
    PRINT 'Table [dbo].[CheckTransactions] created.';
END
GO

-- =============================================================================
-- PAYROLL TABLES
-- =============================================================================

-- JobRoles
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[JobRoles]'))
BEGIN
    CREATE TABLE [dbo].[JobRoles]
    (
        [JobRoleID]   INT            NOT NULL IDENTITY(1,1),
        [ExternalID]  NVARCHAR(50)   NULL,
        [Name]        NVARCHAR(100)  NOT NULL,
        [Description] NVARCHAR(500)  NULL,
        [IsExempt]    BIT            NOT NULL CONSTRAINT [DF_JobRoles_IsExempt]  DEFAULT (0),
        [IsActive]    BIT            NOT NULL CONSTRAINT [DF_JobRoles_IsActive]  DEFAULT (1),
        [CreatedAt]   DATETIME2(0)   NOT NULL CONSTRAINT [DF_JobRoles_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_JobRoles]      PRIMARY KEY CLUSTERED ([JobRoleID] ASC),
        CONSTRAINT [UQ_JobRoles_Name] UNIQUE ([Name])
    );
    PRINT 'Table [dbo].[JobRoles] created.';
END
GO

-- Employees
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[Employees]'))
BEGIN
    CREATE TABLE [dbo].[Employees]
    (
        [EmployeeID]  INT            NOT NULL IDENTITY(1,1),
        [ExternalID]  NVARCHAR(50)   NOT NULL,
        [Name]        NVARCHAR(100)  NOT NULL,
        [Description] NVARCHAR(500)  NULL,
        [IsActive]    BIT            NOT NULL CONSTRAINT [DF_Employees_IsActive]  DEFAULT (1),
        [CreatedAt]   DATETIME2(0)   NOT NULL CONSTRAINT [DF_Employees_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_Employees]       PRIMARY KEY CLUSTERED ([EmployeeID] ASC),
        CONSTRAINT [UQ_Employees_ExtID] UNIQUE ([ExternalID])
    );
    PRINT 'Table [dbo].[Employees] created.';
END
GO

-- EmployeeLocations
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[EmployeeLocations]'))
BEGIN
    CREATE TABLE [dbo].[EmployeeLocations]
    (
        [EmployeeLocationID] INT          NOT NULL IDENTITY(1,1),
        [EmployeeID]         INT          NOT NULL,
        [LocationID]         INT          NOT NULL,
        [CreatedAt]          DATETIME2(0) NOT NULL CONSTRAINT [DF_EmpLoc_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_EmployeeLocations] PRIMARY KEY CLUSTERED ([EmployeeLocationID] ASC),
        CONSTRAINT [UQ_EmployeeLocations] UNIQUE ([EmployeeID],[LocationID]),
        CONSTRAINT [FK_EmpLoc_Employee]   FOREIGN KEY ([EmployeeID])  REFERENCES [dbo].[Employees]([EmployeeID])  ON DELETE CASCADE,
        CONSTRAINT [FK_EmpLoc_Location]   FOREIGN KEY ([LocationID])  REFERENCES [dbo].[Locations]([LocationID])  ON DELETE CASCADE
    );
    CREATE INDEX [IX_EmpLoc_EmployeeID] ON [dbo].[EmployeeLocations]([EmployeeID]);
    CREATE INDEX [IX_EmpLoc_LocationID] ON [dbo].[EmployeeLocations]([LocationID]);
    PRINT 'Table [dbo].[EmployeeLocations] created.';
END
GO

-- =============================================================================
-- DEPARTMENT TABLES
-- =============================================================================

-- Departments
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[Departments]'))
BEGIN
    CREATE TABLE [dbo].[Departments] (
        [DepartmentID] INT           NOT NULL IDENTITY(1,1),
        [Code]         NVARCHAR(20)  NOT NULL,
        [Name]         NVARCHAR(100) NOT NULL,
        [Description]  NVARCHAR(500) NULL,
        [IsActive]     BIT           NOT NULL CONSTRAINT [DF_Dept_Active]  DEFAULT (1),
        [CreatedAt]    DATETIME2(0)  NOT NULL CONSTRAINT [DF_Dept_Created] DEFAULT (GETDATE()),
        CONSTRAINT [PK_Departments]      PRIMARY KEY ([DepartmentID]),
        CONSTRAINT [UQ_Departments_Code] UNIQUE ([Code])
    );
    PRINT 'Table [dbo].[Departments] created.';
END
GO

-- LocationDepartments
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[LocationDepartments]'))
BEGIN
    CREATE TABLE [dbo].[LocationDepartments] (
        [LocationDepartmentID] INT          NOT NULL IDENTITY(1,1),
        [LocationID]           INT          NOT NULL,
        [DepartmentID]         INT          NOT NULL,
        [CreatedAt]            DATETIME2(0) NOT NULL CONSTRAINT [DF_LocDept_Created] DEFAULT (GETDATE()),
        CONSTRAINT [PK_LocationDepartments] PRIMARY KEY ([LocationDepartmentID]),
        CONSTRAINT [UQ_LocationDepartments] UNIQUE ([LocationID],[DepartmentID]),
        CONSTRAINT [FK_LocDept_Location]    FOREIGN KEY ([LocationID])   REFERENCES [dbo].[Locations]([LocationID])     ON DELETE CASCADE,
        CONSTRAINT [FK_LocDept_Department]  FOREIGN KEY ([DepartmentID]) REFERENCES [dbo].[Departments]([DepartmentID]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_LocDept_LocationID]   ON [dbo].[LocationDepartments]([LocationID]);
    CREATE INDEX [IX_LocDept_DepartmentID] ON [dbo].[LocationDepartments]([DepartmentID]);
    PRINT 'Table [dbo].[LocationDepartments] created.';
END
GO

-- =============================================================================
-- PAYROLL BATCH TABLES
-- =============================================================================

-- PayrollBatches
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[PayrollBatches]'))
BEGIN
    CREATE TABLE [dbo].[PayrollBatches] (
        [BatchID]           INT            NOT NULL IDENTITY(1,1),
        [LocationID]        INT            NOT NULL,
        [BatchNameTemplate] NVARCHAR(500)  NOT NULL,
        [PayPeriodLength]   NVARCHAR(20)   NOT NULL,
        [StartDayOfWeek]    NVARCHAR(10)   NULL,
        [IsActive]          BIT            NOT NULL CONSTRAINT [DF_PayrollBatches_IsActive]  DEFAULT (1),
        [CreatedAt]         DATETIME2(0)   NOT NULL CONSTRAINT [DF_PayrollBatches_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_PayrollBatches]           PRIMARY KEY CLUSTERED ([BatchID] ASC),
        CONSTRAINT [FK_PayrollBatches_Locations] FOREIGN KEY ([LocationID])
            REFERENCES [dbo].[Locations]([LocationID])
    );
    PRINT 'Table [dbo].[PayrollBatches] created.';
END
GO

-- PayrollRuns
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[PayrollRuns]'))
BEGIN
    CREATE TABLE [dbo].[PayrollRuns]
    (
        [RunID]          INT             NOT NULL IDENTITY(1,1),
        [BatchID]        INT             NOT NULL,
        [LocationID]     INT             NOT NULL,
        [BatchName]      NVARCHAR(500)   NOT NULL,
        [PayPeriodStart] DATE            NOT NULL,
        [PayPeriodEnd]   DATE            NOT NULL,
        [Status]         NVARCHAR(10)    NOT NULL CONSTRAINT [DF_PayrollRuns_Status]     DEFAULT ('Saved'),
        [GrandTotal]     DECIMAL(18,2)   NOT NULL CONSTRAINT [DF_PayrollRuns_GrandTotal] DEFAULT (0),
        [CreatedAt]      DATETIME2(0)    NOT NULL CONSTRAINT [DF_PayrollRuns_CreatedAt]  DEFAULT (GETDATE()),
        [SubmittedAt]    DATETIME2(0)    NULL,
        [VoidedAt]       DATETIME2(0)    NULL,
        CONSTRAINT [PK_PayrollRuns]           PRIMARY KEY CLUSTERED ([RunID] ASC),
        CONSTRAINT [FK_PayrollRuns_Batches]   FOREIGN KEY ([BatchID])    REFERENCES [dbo].[PayrollBatches]([BatchID]),
        CONSTRAINT [FK_PayrollRuns_Locations] FOREIGN KEY ([LocationID]) REFERENCES [dbo].[Locations]([LocationID]),
        CONSTRAINT [CK_PayrollRuns_Status]    CHECK ([Status] IN ('Saved','Submitted','Voided'))
    );
    PRINT 'Table [dbo].[PayrollRuns] created.';
END
GO

-- PayrollRunLines
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[PayrollRunLines]'))
BEGIN
    CREATE TABLE [dbo].[PayrollRunLines]
    (
        [LineID]      INT             NOT NULL IDENTITY(1,1),
        [RunID]       INT             NOT NULL,
        [EmployeeID]  INT             NOT NULL,
        [JobRoleID]   INT             NOT NULL,
        [PayType]     NVARCHAR(10)    NOT NULL,
        [Quantity]    DECIMAL(10,2)   NOT NULL CONSTRAINT [DF_PayrollRunLines_Quantity]  DEFAULT (1),
        [PayRate]     DECIMAL(18,4)   NOT NULL CONSTRAINT [DF_PayrollRunLines_PayRate]   DEFAULT (0),
        [TotalAmount] DECIMAL(18,2)   NOT NULL CONSTRAINT [DF_PayrollRunLines_Total]     DEFAULT (0),
        [SortOrder]   INT             NOT NULL CONSTRAINT [DF_PayrollRunLines_SortOrder] DEFAULT (0),
        CONSTRAINT [PK_PayrollRunLines]          PRIMARY KEY CLUSTERED ([LineID] ASC),
        CONSTRAINT [FK_PayrollRunLines_Run]      FOREIGN KEY ([RunID])      REFERENCES [dbo].[PayrollRuns]([RunID]),
        CONSTRAINT [FK_PayrollRunLines_Employee] FOREIGN KEY ([EmployeeID]) REFERENCES [dbo].[Employees]([EmployeeID]),
        CONSTRAINT [FK_PayrollRunLines_JobRole]  FOREIGN KEY ([JobRoleID])  REFERENCES [dbo].[JobRoles]([JobRoleID]),
        CONSTRAINT [CK_PayrollRunLines_PayType]  CHECK ([PayType] IN ('Hourly','Exempt'))
    );
    PRINT 'Table [dbo].[PayrollRunLines] created.';
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
