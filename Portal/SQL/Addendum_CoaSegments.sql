-- ============================================================
-- Addendum: CoA Segments + Location.SegmentNumber
-- Run against an existing Culinaire database that already has
-- the Locations table from Addendum_Locations.sql.
-- ============================================================

-- 1. CoaSegments lookup table
IF NOT EXISTS (
    SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CoaSegments]') AND type = 'U'
)
BEGIN
    CREATE TABLE [dbo].[CoaSegments] (
        [SegmentNumber] INT          NOT NULL,
        [Description]   NVARCHAR(100) NOT NULL,
        CONSTRAINT [PK_CoaSegments] PRIMARY KEY CLUSTERED ([SegmentNumber])
    );
END
GO

-- 2. Add SegmentNumber to Locations (idempotent)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID(N'[dbo].[Locations]')
      AND  name = 'SegmentNumber'
)
BEGIN
    ALTER TABLE [dbo].[Locations]
        ADD [SegmentNumber] INT NOT NULL CONSTRAINT [DF_Locations_SegmentNumber] DEFAULT(0);
END
GO
