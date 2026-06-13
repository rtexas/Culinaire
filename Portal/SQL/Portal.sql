-- =============================================================================
-- Culinaire Portal — Database Schema
-- Run this script once against your SQL Server instance (venus-01).
-- All DDL is guarded with IF NOT EXISTS — safe to re-run.
-- =============================================================================

USE [master];
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'Culinaire')
BEGIN
    CREATE DATABASE [Culinaire];
    PRINT 'Database [Culinaire] created.';
END
ELSE
    PRINT 'Database [Culinaire] already exists — skipping CREATE.';
GO

USE [Culinaire];
GO

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
        [LogID]     BIGINT        NOT NULL IDENTITY(1,1),
        [LoggedAt]  DATETIME      NOT NULL CONSTRAINT [DF_Logging_LoggedAt]  DEFAULT (GETDATE()),
        [LogLevel]  INT           NOT NULL CONSTRAINT [DF_Logging_LogLevel]   DEFAULT (2),
        [Message]   NVARCHAR(MAX) NULL,
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
        CONSTRAINT [PK_UserModulePermissions]   PRIMARY KEY CLUSTERED ([PermissionID] ASC),
        CONSTRAINT [FK_UMP_User]   FOREIGN KEY ([UserID])   REFERENCES [dbo].[Users]([UserID])   ON DELETE CASCADE,
        CONSTRAINT [FK_UMP_Module] FOREIGN KEY ([ModuleID]) REFERENCES [dbo].[Modules]([ModuleID]) ON DELETE CASCADE,
        CONSTRAINT [UQ_UMP_UserModule] UNIQUE ([UserID], [ModuleID]),
        CONSTRAINT [CK_UMP_Level] CHECK ([PermissionLevel] IN ('None','Read','ReadWrite'))
    );
    PRINT 'Table [dbo].[UserModulePermissions] created.';
END
GO

-- ── Seed: Settings ────────────────────────────────────────────────────────────
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

-- ── Seed: Modules ─────────────────────────────────────────────────────────────
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

-- ── Verification ──────────────────────────────────────────────────────────────
SELECT 'Settings'              AS [Table], COUNT(*) AS [Rows] FROM [dbo].[Settings]
UNION ALL SELECT 'Logging',    COUNT(*) FROM [dbo].[Logging]
UNION ALL SELECT 'Users',      COUNT(*) FROM [dbo].[Users]
UNION ALL SELECT 'Modules',    COUNT(*) FROM [dbo].[Modules]
UNION ALL SELECT 'UserModulePermissions', COUNT(*) FROM [dbo].[UserModulePermissions];
GO
