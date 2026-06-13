-- =============================================================================
-- Culinaire Portal — Accounts Addendum
-- Run against venus-01 / Culinaire after Portal.sql has been applied.
-- All DDL is guarded — safe to re-run.
-- =============================================================================

USE [Culinaire];
GO

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
        CONSTRAINT [PK_AccountCategories]    PRIMARY KEY CLUSTERED ([CategoryID] ASC),
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
        CONSTRAINT [PK_AccountTypes]    PRIMARY KEY CLUSTERED ([TypeID] ASC),
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
        CONSTRAINT [FK_CoA_Category] FOREIGN KEY ([CategoryID])
            REFERENCES [dbo].[AccountCategories]([CategoryID]) ON DELETE SET NULL,
        CONSTRAINT [FK_CoA_Type] FOREIGN KEY ([TypeID])
            REFERENCES [dbo].[AccountTypes]([TypeID]) ON DELETE SET NULL
    );
    PRINT 'Table [dbo].[ChartOfAccounts] created.';
END
GO

-- ── Verification ──────────────────────────────────────────────────────────────
SELECT 'AccountCategories' AS [Table], COUNT(*) AS [Rows] FROM [dbo].[AccountCategories]
UNION ALL SELECT 'AccountTypes',   COUNT(*) FROM [dbo].[AccountTypes]
UNION ALL SELECT 'ChartOfAccounts',COUNT(*) FROM [dbo].[ChartOfAccounts];
GO
