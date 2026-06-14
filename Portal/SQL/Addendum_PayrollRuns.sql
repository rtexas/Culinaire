-- =============================================================================
-- Culinaire Portal — Payroll Runs Addendum
-- Safe to re-run: all DDL is guarded with IF NOT EXISTS.
-- =============================================================================

USE [Culinaire];
GO

-- ── PayrollRuns (batch instance headers) ─────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[PayrollRuns]'))
BEGIN
    CREATE TABLE [dbo].[PayrollRuns]
    (
        [RunID]          INT             NOT NULL IDENTITY(1,1),
        [BatchID]        INT             NOT NULL,
        [LocationID]     INT             NOT NULL,
        [BatchName]      NVARCHAR(500)   NOT NULL,
        [PayPeriodStart] DATE            NOT NULL,
        [PayPeriodEnd]   DATE            NOT NULL,
        [Status]         NVARCHAR(10)    NOT NULL CONSTRAINT [DF_PayrollRuns_Status]      DEFAULT ('Saved'),
        [GrandTotal]     DECIMAL(18,2)   NOT NULL CONSTRAINT [DF_PayrollRuns_GrandTotal]  DEFAULT (0),
        [CreatedAt]      DATETIME2(0)    NOT NULL CONSTRAINT [DF_PayrollRuns_CreatedAt]   DEFAULT (GETDATE()),
        [SubmittedAt]    DATETIME2(0)    NULL,
        [VoidedAt]       DATETIME2(0)    NULL,
        CONSTRAINT [PK_PayrollRuns]              PRIMARY KEY CLUSTERED ([RunID] ASC),
        CONSTRAINT [FK_PayrollRuns_Batches]      FOREIGN KEY ([BatchID])
            REFERENCES [dbo].[PayrollBatches] ([BatchID]),
        CONSTRAINT [FK_PayrollRuns_Locations]    FOREIGN KEY ([LocationID])
            REFERENCES [dbo].[Locations] ([LocationID]),
        CONSTRAINT [CK_PayrollRuns_Status]       CHECK ([Status] IN ('Saved','Submitted','Voided'))
    );
    PRINT 'Table [dbo].[PayrollRuns] created.';
END
ELSE
    PRINT 'Table [dbo].[PayrollRuns] already exists.';
GO

-- ── PayrollRunLines (line items within each run) ──────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[PayrollRunLines]'))
BEGIN
    CREATE TABLE [dbo].[PayrollRunLines]
    (
        [LineID]       INT             NOT NULL IDENTITY(1,1),
        [RunID]        INT             NOT NULL,
        [EmployeeID]   INT             NOT NULL,
        [JobRoleID]    INT             NOT NULL,
        [PayType]      NVARCHAR(10)    NOT NULL,
        [Quantity]     DECIMAL(10,2)   NOT NULL CONSTRAINT [DF_PayrollRunLines_Quantity]  DEFAULT (1),
        [PayRate]      DECIMAL(18,4)   NOT NULL CONSTRAINT [DF_PayrollRunLines_PayRate]   DEFAULT (0),
        [TotalAmount]  DECIMAL(18,2)   NOT NULL CONSTRAINT [DF_PayrollRunLines_Total]     DEFAULT (0),
        [SortOrder]    INT             NOT NULL CONSTRAINT [DF_PayrollRunLines_SortOrder] DEFAULT (0),
        CONSTRAINT [PK_PayrollRunLines]           PRIMARY KEY CLUSTERED ([LineID] ASC),
        CONSTRAINT [FK_PayrollRunLines_Run]       FOREIGN KEY ([RunID])
            REFERENCES [dbo].[PayrollRuns] ([RunID]),
        CONSTRAINT [FK_PayrollRunLines_Employee]  FOREIGN KEY ([EmployeeID])
            REFERENCES [dbo].[Employees] ([EmployeeID]),
        CONSTRAINT [FK_PayrollRunLines_JobRole]   FOREIGN KEY ([JobRoleID])
            REFERENCES [dbo].[JobRoles] ([JobRoleID]),
        CONSTRAINT [CK_PayrollRunLines_PayType]   CHECK ([PayType] IN ('Hourly','Exempt'))
    );
    PRINT 'Table [dbo].[PayrollRunLines] created.';
END
ELSE
    PRINT 'Table [dbo].[PayrollRunLines] already exists.';
GO

SELECT 'Payroll Runs addendum complete.' AS [Status];
GO
