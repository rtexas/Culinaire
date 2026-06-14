-- =============================================================================
-- Culinaire Portal — COA Segment Definitions
-- COA account structure: Dept — Location — Natural Account
--   Segment 1 = Department  (e.g. 001)
--   Segment 2 = Location    (e.g. 100)
--   Segment 3 = Natural Account (e.g. 2100)
-- Safe to re-run.
-- =============================================================================

USE [Culinaire];
GO

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

-- Ensure all locations are set to segment 2
UPDATE [dbo].[Locations] SET [SegmentNumber] = 2 WHERE [SegmentNumber] != 2;

SELECT 'COA segment definitions applied.' AS [Status];
GO
