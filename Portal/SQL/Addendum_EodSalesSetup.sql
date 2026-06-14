-- ============================================================
-- Addendum: EOD Sales Setup and Entry tables
-- Includes: ChartOfAccounts, EodColumns.SegmentValue,
--           EodSetups/Columns/Rows/Cells, EodSalesEntries/Values
-- Safe to run against an existing Culinaire database.
-- ============================================================

-- ── CoA Account Delimiter setting ────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [Name] = 'CoaAccountDelimiter')
BEGIN
    INSERT INTO [dbo].[Settings]([Name],[Value],[IsEnabled]) VALUES('CoaAccountDelimiter','-',1);
    PRINT 'Setting [CoaAccountDelimiter] added.';
END
GO

-- ── ChartOfAccounts — add segment columns if missing ─────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ChartOfAccounts]') AND name = 'FullAccountString')
BEGIN
    ALTER TABLE [dbo].[ChartOfAccounts] ADD
        [FullAccountString] NVARCHAR(200) NULL,
        [Seg1Value]         NVARCHAR(50)  NULL,
        [Seg2Value]         NVARCHAR(50)  NULL,
        [Seg3Value]         NVARCHAR(50)  NULL,
        [Seg4Value]         NVARCHAR(50)  NULL,
        [Seg5Value]         NVARCHAR(50)  NULL,
        [Seg6Value]         NVARCHAR(50)  NULL;
    PRINT 'Segment columns added to [dbo].[ChartOfAccounts].';
END
GO

-- ── EodColumns.SegmentValue ───────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[EodColumns]') AND name = 'SegmentValue')
BEGIN
    ALTER TABLE [dbo].[EodColumns] ADD [SegmentValue] NVARCHAR(50) NULL;
    PRINT 'Column [SegmentValue] added to [dbo].[EodColumns].';
END
GO

-- ── EodSetups ─────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EodSetups]') AND type = 'U')
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

-- ── EodSetupColumns ───────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EodSetupColumns]') AND type = 'U')
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

-- ── EodSetupRows ──────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EodSetupRows]') AND type = 'U')
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

-- ── EodSetupCells ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EodSetupCells]') AND type = 'U')
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

-- ── EodSalesEntries ───────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EodSalesEntries]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[EodSalesEntries] (
        [EntryID]     INT          NOT NULL IDENTITY(1,1),
        [EntryDate]   DATE         NOT NULL,
        [LocationID]  INT          NOT NULL,
        [SetupID]     INT          NOT NULL,
        [IsSubmitted] BIT          NOT NULL CONSTRAINT [DF_EodEntries_Sub]       DEFAULT (0),
        [SubmittedAt] DATETIME2(0) NULL,
        [CreatedAt]   DATETIME2(0) NOT NULL CONSTRAINT [DF_EodEntries_CreatedAt] DEFAULT (GETDATE()),
        [UpdatedAt]   DATETIME2(0) NOT NULL CONSTRAINT [DF_EodEntries_UpdatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_EodSalesEntries] PRIMARY KEY CLUSTERED ([EntryID] ASC),
        CONSTRAINT [UQ_EodSalesEntries] UNIQUE ([EntryDate],[LocationID],[SetupID])
    );
    PRINT 'Table [dbo].[EodSalesEntries] created.';
END
GO

-- ── EodSalesValues ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EodSalesValues]') AND type = 'U')
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
