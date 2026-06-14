USE [Culinaire];
GO

-- Add address fields to Locations
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Locations]') AND name = 'Address1')
BEGIN
    ALTER TABLE [dbo].[Locations] ADD [Address1] NVARCHAR(100) NULL;
    ALTER TABLE [dbo].[Locations] ADD [Address2] NVARCHAR(100) NULL;
    ALTER TABLE [dbo].[Locations] ADD [City]     NVARCHAR(100) NULL;
    ALTER TABLE [dbo].[Locations] ADD [State]    NVARCHAR(50)  NULL;
    ALTER TABLE [dbo].[Locations] ADD [Zip]      NVARCHAR(20)  NULL;
    PRINT 'Address columns added to [dbo].[Locations].';
END
GO

-- CheckSetupVendors
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[CheckSetupVendors]'))
BEGIN
    CREATE TABLE [dbo].[CheckSetupVendors] (
        [CheckSetupVendorID] INT NOT NULL IDENTITY(1,1),
        [LocationID]         INT NOT NULL,
        [VendorID]           INT NOT NULL,
        [IsActive]           BIT NOT NULL CONSTRAINT [DF_CSV_Active] DEFAULT(1),
        CONSTRAINT [PK_CheckSetupVendors] PRIMARY KEY([CheckSetupVendorID]),
        CONSTRAINT [UQ_CheckSetupVendors] UNIQUE([LocationID],[VendorID]),
        CONSTRAINT [FK_CSV_Location] FOREIGN KEY([LocationID]) REFERENCES [dbo].[Locations]([LocationID]) ON DELETE CASCADE,
        CONSTRAINT [FK_CSV_Vendor]   FOREIGN KEY([VendorID])   REFERENCES [dbo].[Vendors]([VendorID])   ON DELETE CASCADE
    );
    PRINT 'Table [dbo].[CheckSetupVendors] created.';
END
GO

-- CheckSetupAccounts
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[CheckSetupAccounts]'))
BEGIN
    CREATE TABLE [dbo].[CheckSetupAccounts] (
        [CheckSetupAccountID] INT NOT NULL IDENTITY(1,1),
        [LocationID]          INT NOT NULL,
        [AccountID]           INT NOT NULL,
        [IsActive]            BIT NOT NULL CONSTRAINT [DF_CSA_Active] DEFAULT(1),
        CONSTRAINT [PK_CheckSetupAccounts] PRIMARY KEY([CheckSetupAccountID]),
        CONSTRAINT [UQ_CheckSetupAccounts] UNIQUE([LocationID],[AccountID]),
        CONSTRAINT [FK_CSA_Location] FOREIGN KEY([LocationID]) REFERENCES [dbo].[Locations]([LocationID]) ON DELETE CASCADE,
        CONSTRAINT [FK_CSA_Account]  FOREIGN KEY([AccountID])  REFERENCES [dbo].[ChartOfAccounts]([AccountID]) ON DELETE CASCADE
    );
    PRINT 'Table [dbo].[CheckSetupAccounts] created.';
END
GO

-- CheckTransactions
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[CheckTransactions]'))
BEGIN
    CREATE TABLE [dbo].[CheckTransactions] (
        [CheckTransactionID]   INT            NOT NULL IDENTITY(1,1),
        [LocationID]           INT            NOT NULL,
        [CheckNumber]          INT            NOT NULL DEFAULT(0),
        [TransactionDate]      DATE           NOT NULL,
        [VendorID]             INT            NULL,
        [IsManualVendor]       BIT            NOT NULL CONSTRAINT [DF_CT_Manual] DEFAULT(0),
        [ManualVendorName]     NVARCHAR(100)  NULL,
        [ManualVendorAddress1] NVARCHAR(100)  NULL,
        [ManualVendorAddress2] NVARCHAR(100)  NULL,
        [ManualVendorCity]     NVARCHAR(100)  NULL,
        [ManualVendorState]    NVARCHAR(50)   NULL,
        [ManualVendorZip]      NVARCHAR(20)   NULL,
        [Amount]               DECIMAL(18,2)  NOT NULL,
        [Memo]                 NVARCHAR(500)  NULL,
        [ExpenseAccountID]     INT            NULL,
        [IsSubmitted]          BIT            NOT NULL CONSTRAINT [DF_CT_Submitted] DEFAULT(0),
        [SubmittedAt]          DATETIME2(0)   NULL,
        [CreatedByUserID]      INT            NULL,
        [CreatedAt]            DATETIME2(0)   NOT NULL CONSTRAINT [DF_CT_Created] DEFAULT(GETDATE()),
        CONSTRAINT [PK_CheckTransactions] PRIMARY KEY([CheckTransactionID]),
        CONSTRAINT [FK_CT_Location] FOREIGN KEY([LocationID]) REFERENCES [dbo].[Locations]([LocationID]),
        CONSTRAINT [FK_CT_Vendor]   FOREIGN KEY([VendorID])   REFERENCES [dbo].[Vendors]([VendorID]),
        CONSTRAINT [FK_CT_Account]  FOREIGN KEY([ExpenseAccountID]) REFERENCES [dbo].[ChartOfAccounts]([AccountID])
    );
    CREATE INDEX [IX_CT_Location] ON [dbo].[CheckTransactions]([LocationID],[TransactionDate] DESC);
    PRINT 'Table [dbo].[CheckTransactions] created.';
END
GO

-- Add new audit/status columns to CheckTransactions
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[CheckTransactions]') AND name = 'IsVoided')
BEGIN
    ALTER TABLE [dbo].[CheckTransactions] ADD [IsVoided]          BIT          NOT NULL CONSTRAINT [DF_CT_Voided]   DEFAULT(0);
    ALTER TABLE [dbo].[CheckTransactions] ADD [VoidedAt]          DATETIME2(0) NULL;
    ALTER TABLE [dbo].[CheckTransactions] ADD [VoidedByUserID]    INT          NULL;
    ALTER TABLE [dbo].[CheckTransactions] ADD [SubmittedByUserID] INT          NULL;
    PRINT 'Void/Submit audit columns added to [dbo].[CheckTransactions].';
END
GO

SELECT 'Checks addendum complete.' AS [Status];
GO
