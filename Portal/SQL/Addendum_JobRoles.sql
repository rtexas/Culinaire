-- =============================================================================
-- Culinaire Portal — Job Roles Addendum
-- Safe to re-run: all DDL is guarded with IF NOT EXISTS.
-- =============================================================================

USE [Culinaire];
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[JobRoles]'))
BEGIN
    CREATE TABLE [dbo].[JobRoles]
    (
        [JobRoleID]   INT            NOT NULL IDENTITY(1,1),
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
ELSE
    PRINT 'Table [dbo].[JobRoles] already exists.';
GO

-- Add ExternalID to JobRoles if not present
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[JobRoles]') AND name = 'ExternalID')
BEGIN
    ALTER TABLE [dbo].[JobRoles] ADD [ExternalID] NVARCHAR(50) NULL;
    PRINT 'ExternalID column added to [dbo].[JobRoles].';
END
GO

SELECT 'Job Roles addendum complete.' AS [Status];
GO
