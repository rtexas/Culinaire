USE [Culinaire];
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[Departments]'))
BEGIN
    CREATE TABLE [dbo].[Departments] (
        [DepartmentID] INT           NOT NULL IDENTITY(1,1),
        [Code]         NVARCHAR(20)  NOT NULL,
        [Name]         NVARCHAR(100) NOT NULL,
        [Description]  NVARCHAR(500) NULL,
        [IsActive]     BIT           NOT NULL CONSTRAINT [DF_Dept_Active]   DEFAULT(1),
        [CreatedAt]    DATETIME2(0)  NOT NULL CONSTRAINT [DF_Dept_Created]  DEFAULT(GETDATE()),
        CONSTRAINT [PK_Departments]      PRIMARY KEY([DepartmentID]),
        CONSTRAINT [UQ_Departments_Code] UNIQUE([Code])
    );
    PRINT 'Table [dbo].[Departments] created.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[LocationDepartments]'))
BEGIN
    CREATE TABLE [dbo].[LocationDepartments] (
        [LocationDepartmentID] INT          NOT NULL IDENTITY(1,1),
        [LocationID]           INT          NOT NULL,
        [DepartmentID]         INT          NOT NULL,
        [CreatedAt]            DATETIME2(0) NOT NULL CONSTRAINT [DF_LocDept_Created] DEFAULT(GETDATE()),
        CONSTRAINT [PK_LocationDepartments] PRIMARY KEY([LocationDepartmentID]),
        CONSTRAINT [UQ_LocationDepartments] UNIQUE([LocationID],[DepartmentID]),
        CONSTRAINT [FK_LocDept_Location]    FOREIGN KEY([LocationID])   REFERENCES [dbo].[Locations]([LocationID])   ON DELETE CASCADE,
        CONSTRAINT [FK_LocDept_Department]  FOREIGN KEY([DepartmentID]) REFERENCES [dbo].[Departments]([DepartmentID]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_LocDept_LocationID]   ON [dbo].[LocationDepartments]([LocationID]);
    CREATE INDEX [IX_LocDept_DepartmentID] ON [dbo].[LocationDepartments]([DepartmentID]);
    PRINT 'Table [dbo].[LocationDepartments] created.';
END
GO

SELECT 'Departments addendum complete.' AS [Status];
GO
