-- =============================================================================
-- Culinaire Portal — Employees Addendum
-- Safe to re-run: all DDL is guarded with IF NOT EXISTS.
-- =============================================================================

USE [Culinaire];
GO

-- ── Employees ─────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[Employees]'))
BEGIN
    CREATE TABLE [dbo].[Employees]
    (
        [EmployeeID]  INT            NOT NULL IDENTITY(1,1),
        [ExternalID]  NVARCHAR(50)   NOT NULL,
        [Name]        NVARCHAR(100)  NOT NULL,
        [Description] NVARCHAR(500)  NULL,
        [IsActive]    BIT            NOT NULL CONSTRAINT [DF_Employees_IsActive]  DEFAULT (1),
        [CreatedAt]   DATETIME2(0)   NOT NULL CONSTRAINT [DF_Employees_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_Employees]          PRIMARY KEY CLUSTERED ([EmployeeID] ASC),
        CONSTRAINT [UQ_Employees_ExtID]    UNIQUE ([ExternalID])
    );
    PRINT 'Table [dbo].[Employees] created.';
END
ELSE
    PRINT 'Table [dbo].[Employees] already exists.';
GO

-- ── EmployeeLocations ─────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[EmployeeLocations]'))
BEGIN
    CREATE TABLE [dbo].[EmployeeLocations]
    (
        [EmployeeLocationID] INT          NOT NULL IDENTITY(1,1),
        [EmployeeID]         INT          NOT NULL,
        [LocationID]         INT          NOT NULL,
        [CreatedAt]          DATETIME2(0) NOT NULL CONSTRAINT [DF_EmpLoc_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_EmployeeLocations]  PRIMARY KEY CLUSTERED ([EmployeeLocationID] ASC),
        CONSTRAINT [UQ_EmployeeLocations]  UNIQUE ([EmployeeID],[LocationID]),
        CONSTRAINT [FK_EmpLoc_Employee]
            FOREIGN KEY ([EmployeeID])  REFERENCES [dbo].[Employees]([EmployeeID])  ON DELETE CASCADE,
        CONSTRAINT [FK_EmpLoc_Location]
            FOREIGN KEY ([LocationID]) REFERENCES [dbo].[Locations]([LocationID])  ON DELETE CASCADE
    );
    CREATE INDEX [IX_EmpLoc_EmployeeID] ON [dbo].[EmployeeLocations]([EmployeeID]);
    CREATE INDEX [IX_EmpLoc_LocationID] ON [dbo].[EmployeeLocations]([LocationID]);
    PRINT 'Table [dbo].[EmployeeLocations] created.';
END
ELSE
    PRINT 'Table [dbo].[EmployeeLocations] already exists.';
GO

SELECT 'Employees addendum complete.' AS [Status];
GO
