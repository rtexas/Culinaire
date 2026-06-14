-- =============================================================================
-- Culinaire Portal — Payroll Batches Addendum
-- Safe to re-run: all DDL is guarded with IF NOT EXISTS.
-- =============================================================================

USE [Culinaire];
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[PayrollBatches]'))
BEGIN
    CREATE TABLE [dbo].[PayrollBatches]
    (
        [BatchID]            INT            NOT NULL IDENTITY(1,1),
        [LocationID]         INT            NOT NULL,
        [BatchNameTemplate]  NVARCHAR(500)  NOT NULL,
        [PayPeriodLength]    NVARCHAR(20)   NOT NULL,
        [StartDayOfWeek]     NVARCHAR(10)   NULL,
        [IsActive]           BIT            NOT NULL CONSTRAINT [DF_PayrollBatches_IsActive]  DEFAULT (1),
        [CreatedAt]          DATETIME2(0)   NOT NULL CONSTRAINT [DF_PayrollBatches_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_PayrollBatches]             PRIMARY KEY CLUSTERED ([BatchID] ASC),
        CONSTRAINT [FK_PayrollBatches_Locations]   FOREIGN KEY ([LocationID])
            REFERENCES [dbo].[Locations] ([LocationID])
    );
    PRINT 'Table [dbo].[PayrollBatches] created.';
END
ELSE
    PRINT 'Table [dbo].[PayrollBatches] already exists.';
GO

SELECT 'Payroll Batches addendum complete.' AS [Status];
GO
