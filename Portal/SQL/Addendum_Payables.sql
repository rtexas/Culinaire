-- =============================================================================
-- Culinaire Portal — Addendum: Payables module
-- Adds ShippingMethods, PayableHeaders, and PayableLineItems tables.
-- Run against an existing [Culinaire] database.
-- Safe to re-run: all DDL is guarded with IF NOT EXISTS.
-- =============================================================================

USE [Culinaire];
GO

-- ── ShippingMethods ───────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[ShippingMethods]'))
BEGIN
    CREATE TABLE [dbo].[ShippingMethods]
    (
        [ShippingMethodID] INT           NOT NULL IDENTITY(1,1),
        [Name]             NVARCHAR(100) NOT NULL,
        [Description]      NVARCHAR(500) NULL,
        [IsActive]         BIT           NOT NULL CONSTRAINT [DF_ShipMethods_IsActive] DEFAULT (1),
        CONSTRAINT [PK_ShippingMethods]      PRIMARY KEY CLUSTERED ([ShippingMethodID] ASC),
        CONSTRAINT [UQ_ShippingMethods_Name] UNIQUE ([Name])
    );
    PRINT 'Table [dbo].[ShippingMethods] created.';
END
ELSE
    PRINT 'Table [dbo].[ShippingMethods] already exists.';
GO

-- ── PayableHeaders ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[PayableHeaders]'))
BEGIN
    CREATE TABLE [dbo].[PayableHeaders]
    (
        [PayableID]        INT            NOT NULL IDENTITY(1,1),
        [VendorID]         INT            NOT NULL,
        [InvoiceNumber]    NVARCHAR(100)  NOT NULL,
        [InvoiceDate]      DATE           NOT NULL,
        [DueDate]          DATE           NULL,
        [ShippingMethodID] INT            NULL,
        [ShippingCharge]   DECIMAL(18,4)  NOT NULL CONSTRAINT [DF_PayHdr_ShipCharge] DEFAULT (0),
        [TaxAmount]        DECIMAL(18,4)  NOT NULL CONSTRAINT [DF_PayHdr_TaxAmount]  DEFAULT (0),
        [Notes]            NVARCHAR(500)  NULL,
        [Status]           NVARCHAR(20)   NOT NULL CONSTRAINT [DF_PayHdr_Status]     DEFAULT ('Open'),
        [CreatedAt]        DATETIME       NOT NULL CONSTRAINT [DF_PayHdr_CreatedAt]  DEFAULT (GETDATE()),
        [UpdatedAt]        DATETIME       NOT NULL CONSTRAINT [DF_PayHdr_UpdatedAt]  DEFAULT (GETDATE()),
        CONSTRAINT [PK_PayableHeaders] PRIMARY KEY CLUSTERED ([PayableID] ASC),
        CONSTRAINT [FK_PayHdr_Vendor]
            FOREIGN KEY ([VendorID]) REFERENCES [dbo].[Vendors]([VendorID]),
        CONSTRAINT [FK_PayHdr_ShippingMethod]
            FOREIGN KEY ([ShippingMethodID]) REFERENCES [dbo].[ShippingMethods]([ShippingMethodID])
            ON DELETE SET NULL
    );
    PRINT 'Table [dbo].[PayableHeaders] created.';
END
ELSE
    PRINT 'Table [dbo].[PayableHeaders] already exists.';
GO

-- ── PayableLineItems ──────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[PayableLineItems]'))
BEGIN
    CREATE TABLE [dbo].[PayableLineItems]
    (
        [LineItemID]    INT            NOT NULL IDENTITY(1,1),
        [PayableID]     INT            NOT NULL,
        [LineNumber]    INT            NOT NULL,
        [ItemID]        INT            NULL,
        [Description]   NVARCHAR(500)  NOT NULL,
        [Quantity]      DECIMAL(18,4)  NOT NULL CONSTRAINT [DF_PayLine_Qty]   DEFAULT (1),
        [UnitPrice]     DECIMAL(18,4)  NOT NULL CONSTRAINT [DF_PayLine_Price] DEFAULT (0),
        [ExtendedPrice] DECIMAL(18,4)  NOT NULL CONSTRAINT [DF_PayLine_Ext]   DEFAULT (0),
        CONSTRAINT [PK_PayableLineItems] PRIMARY KEY CLUSTERED ([LineItemID] ASC),
        CONSTRAINT [FK_PayLine_Payable]
            FOREIGN KEY ([PayableID]) REFERENCES [dbo].[PayableHeaders]([PayableID])
            ON DELETE CASCADE,
        CONSTRAINT [FK_PayLine_Item]
            FOREIGN KEY ([ItemID]) REFERENCES [dbo].[Items]([ItemID])
            ON DELETE SET NULL
    );
    CREATE INDEX [IX_PayableLineItems_PayableID] ON [dbo].[PayableLineItems]([PayableID]);
    PRINT 'Table [dbo].[PayableLineItems] created.';
END
ELSE
    PRINT 'Table [dbo].[PayableLineItems] already exists.';
GO
