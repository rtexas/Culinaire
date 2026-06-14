-- ============================================================
-- Addendum: EOD Sales setup tables (Rows, Columns, Sections)
-- Safe to run against an existing Culinaire database.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EodRows]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[EodRows] (
        [RowID]       INT            NOT NULL IDENTITY(1,1),
        [Name]        NVARCHAR(200)  NOT NULL,
        [Description] NVARCHAR(500)  NULL,
        [SectionID]   INT            NOT NULL CONSTRAINT [DF_EodRows_SectionID] DEFAULT (0),
        [CreatedAt]   DATETIME2(0)   NOT NULL CONSTRAINT [DF_EodRows_CreatedAt]  DEFAULT (GETDATE()),
        CONSTRAINT [PK_EodRows]      PRIMARY KEY CLUSTERED ([RowID] ASC),
        CONSTRAINT [UQ_EodRows_Name] UNIQUE ([Name])
    );
    PRINT 'Table [dbo].[EodRows] created.';
END
ELSE IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[EodRows]') AND name = 'SectionID')
BEGIN
    ALTER TABLE [dbo].[EodRows] ADD [SectionID] INT NOT NULL CONSTRAINT [DF_EodRows_SectionID] DEFAULT (0);
    PRINT 'Column [SectionID] added to [dbo].[EodRows].';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EodColumns]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[EodColumns] (
        [ColumnID]         INT            NOT NULL IDENTITY(1,1),
        [Name]             NVARCHAR(200)  NOT NULL,
        [Description]      NVARCHAR(500)  NULL,
        [CoaSegmentNumber] INT            NOT NULL CONSTRAINT [DF_EodColumns_CoaSeg] DEFAULT (0),
        [CreatedAt]        DATETIME2(0)   NOT NULL CONSTRAINT [DF_EodColumns_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_EodColumns]      PRIMARY KEY CLUSTERED ([ColumnID] ASC),
        CONSTRAINT [UQ_EodColumns_Name] UNIQUE ([Name])
    );
    PRINT 'Table [dbo].[EodColumns] created.';
END
ELSE IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[EodColumns]') AND name = 'CoaSegmentNumber')
BEGIN
    ALTER TABLE [dbo].[EodColumns] ADD [CoaSegmentNumber] INT NOT NULL CONSTRAINT [DF_EodColumns_CoaSeg] DEFAULT (0);
    PRINT 'Column [CoaSegmentNumber] added to [dbo].[EodColumns].';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EodSections]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[EodSections] (
        [SectionID]   INT            NOT NULL IDENTITY(1,1),
        [Name]        NVARCHAR(200)  NOT NULL,
        [Description] NVARCHAR(500)  NULL,
        [Multiplier]  INT            NOT NULL CONSTRAINT [DF_EodSections_Multiplier] DEFAULT (1),
        [CreatedAt]   DATETIME2(0)   NOT NULL CONSTRAINT [DF_EodSections_CreatedAt]  DEFAULT (GETDATE()),
        CONSTRAINT [PK_EodSections]      PRIMARY KEY CLUSTERED ([SectionID] ASC),
        CONSTRAINT [UQ_EodSections_Name] UNIQUE ([Name]),
        CONSTRAINT [CK_EodSections_Mult] CHECK ([Multiplier] IN (-1, 0, 1))
    );
    PRINT 'Table [dbo].[EodSections] created.';
END
GO
