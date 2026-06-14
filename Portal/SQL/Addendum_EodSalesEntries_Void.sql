-- =============================================================================
-- Culinaire Portal — EodSalesEntries: add IsVoided / VoidedAt columns
-- Safe to re-run: all DDL is guarded with IF NOT EXISTS.
-- =============================================================================

USE [Culinaire];
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[dbo].[EodSalesEntries]')
                 AND name = 'IsVoided')
BEGIN
    ALTER TABLE [dbo].[EodSalesEntries]
        ADD [IsVoided]  BIT          NOT NULL CONSTRAINT [DF_EodEntries_IsVoided] DEFAULT (0),
            [VoidedAt]  DATETIME2(0) NULL;
    PRINT 'Columns [IsVoided], [VoidedAt] added to [dbo].[EodSalesEntries].';
END
ELSE
    PRINT 'Columns already exist — skipping.';
GO

SELECT 'EodSalesEntries Void addendum complete.' AS [Status];
GO
