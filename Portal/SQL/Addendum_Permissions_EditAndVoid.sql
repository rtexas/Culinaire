-- Addendum_Permissions_EditAndVoid.sql
-- Extends the PermissionLevel check constraint to include 'EditAndVoid'.
-- Safe to re-run.

PRINT 'Addendum_Permissions_EditAndVoid: starting...';
GO

-- Drop existing check constraint if it does not already include EditAndVoid
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE  name = 'CK_UMP_Level'
    AND    parent_object_id = OBJECT_ID(N'[dbo].[UserModulePermissions]')
    AND    [definition] NOT LIKE '%EditAndVoid%'
)
BEGIN
    ALTER TABLE [dbo].[UserModulePermissions] DROP CONSTRAINT [CK_UMP_Level];
    PRINT '  Dropped old CK_UMP_Level constraint.';
END
GO

-- Add updated constraint if it does not exist yet
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE  name = 'CK_UMP_Level'
    AND    parent_object_id = OBJECT_ID(N'[dbo].[UserModulePermissions]')
)
BEGIN
    ALTER TABLE [dbo].[UserModulePermissions]
    ADD CONSTRAINT [CK_UMP_Level]
        CHECK ([PermissionLevel] IN ('None','Read','ReadWrite','EditAndVoid'));
    PRINT '  Added CK_UMP_Level with EditAndVoid included.';
END
ELSE
BEGIN
    PRINT '  CK_UMP_Level already includes EditAndVoid — skipped.';
END
GO

SELECT 'Addendum_Permissions_EditAndVoid complete.' AS [Status];
