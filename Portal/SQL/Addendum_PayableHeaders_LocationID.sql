-- =============================================================================
-- Culinaire Portal — PayableHeaders: add LocationID + align Status values
-- Safe to re-run: all DDL is guarded with IF NOT EXISTS / IF EXISTS.
-- =============================================================================

USE [Culinaire];
GO

-- ── Add LocationID column ─────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[dbo].[PayableHeaders]')
                 AND name = 'LocationID')
BEGIN
    ALTER TABLE [dbo].[PayableHeaders]
        ADD [LocationID] INT NULL
            CONSTRAINT [FK_PayHdr_Location]
            REFERENCES [dbo].[Locations]([LocationID]);
    PRINT 'Column [LocationID] added to [dbo].[PayableHeaders].';
END
ELSE
    PRINT 'Column [LocationID] already exists.';
GO

-- ── Change default Status from ''Open'' to ''Saved'' ──────────────────────────
IF EXISTS (SELECT 1 FROM sys.default_constraints
           WHERE parent_object_id = OBJECT_ID(N'[dbo].[PayableHeaders]')
             AND name = 'DF_PayHdr_Status')
BEGIN
    ALTER TABLE [dbo].[PayableHeaders] DROP CONSTRAINT [DF_PayHdr_Status];
    ALTER TABLE [dbo].[PayableHeaders]
        ADD CONSTRAINT [DF_PayHdr_Status] DEFAULT ('Saved') FOR [Status];
    PRINT 'Default constraint [DF_PayHdr_Status] updated to ''Saved''.';
END
ELSE
    PRINT 'Constraint [DF_PayHdr_Status] not found — skipping.';
GO

-- ── Migrate existing ''Open'' records to ''Saved'' ────────────────────────────
UPDATE [dbo].[PayableHeaders] SET [Status] = 'Saved' WHERE [Status] = 'Open';
PRINT CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' row(s) migrated from ''Open'' to ''Saved''.';
GO

SELECT 'PayableHeaders LocationID addendum complete.' AS [Status];
GO
