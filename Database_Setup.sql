-- ==========================================================
-- SQL Server Setup Script for WebMPS
-- Compatible with SSMS (SQL Server Express / Standard)
-- ==========================================================

-- 1. Create Database
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'WebMPS')
BEGIN
    CREATE DATABASE [WebMPS];
END
GO

USE [WebMPS];
GO

-- 2. Table: UploadRecords (Tracks uploaded PDF files)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UploadRecords]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[UploadRecords] (
        [Id] NVARCHAR(50) NOT NULL PRIMARY KEY,
        [FileName] NVARCHAR(255) NOT NULL,
        [FileSizeBytes] BIGINT NOT NULL,
        [TotalSlips] INT NOT NULL,
        [UploadedAt] DATETIME2 NOT NULL,
        [UploadedBy] NVARCHAR(100) NULL,
        [Status] NVARCHAR(50) NOT NULL
    );
END
GO

-- 3. Table: MpsSlips (Individual Material Picking Slips)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MpsSlips]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[MpsSlips] (
        [Id] NVARCHAR(50) NOT NULL PRIMARY KEY,
        [UploadId] NVARCHAR(50) NULL,
        [OriginalPage] INT NOT NULL,
        [Quadrant] INT NOT NULL,
        [Title] NVARCHAR(255) NULL,
        [Tag] NVARCHAR(50) NULL,
        [Planner] NVARCHAR(100) NULL,
        [SlipNumber] NVARCHAR(100) NULL,
        [SupplyDate] NVARCHAR(50) NULL,
        [ReqDate] NVARCHAR(50) NULL,
        [FromLocation] NVARCHAR(100) NULL,
        [ToLocation] NVARCHAR(100) NULL,
        [PartCode] NVARCHAR(100) NULL,
        [PartDescription] NVARCHAR(255) NULL,
        [Qty] DECIMAL(18, 3) NOT NULL,
        [Unit] NVARCHAR(50) NULL,
        [IsVmi] BIT NOT NULL,
        [VendorCode] NVARCHAR(100) NULL,
        [VendorName] NVARCHAR(255) NULL,
        [PeggedOrdersJson] NVARCHAR(MAX) NULL,
        [Barcode] NVARCHAR(100) NULL,
        [ApprovalStatus] NVARCHAR(50) NOT NULL DEFAULT N'Pending',
        [DeliverySignature] NVARCHAR(100) NULL,
        [ReceiverSignature] NVARCHAR(100) NULL,
        [ApprovedAt] DATETIME2 NULL,
        [ApprovedBy] NVARCHAR(100) NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [SourceFile] NVARCHAR(255) NOT NULL,
        [IsScanned] BIT NOT NULL DEFAULT 0,
        [ScannedAt] DATETIME2 NULL,
        [ScannedBy] NVARCHAR(100) NULL
    );
END
GO

-- 4. Table: ApprovalLogs (Audit Trail for Approvals & Handovers)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ApprovalLogs]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ApprovalLogs] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [SlipId] NVARCHAR(50) NOT NULL,
        [SlipNumber] NVARCHAR(100) NULL,
        [Action] NVARCHAR(100) NOT NULL,
        [ApproverName] NVARCHAR(100) NOT NULL,
        [Role] NVARCHAR(100) NOT NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [Timestamp] DATETIME2 NOT NULL
    );
END
GO

-- 5. Indexes for Fast Filtering & Searching
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_MpsSlips_SlipNumber' AND object_id = OBJECT_ID(N'[dbo].[MpsSlips]'))
    CREATE INDEX [IX_MpsSlips_SlipNumber] ON [dbo].[MpsSlips]([SlipNumber]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_MpsSlips_FromLocation' AND object_id = OBJECT_ID(N'[dbo].[MpsSlips]'))
    CREATE INDEX [IX_MpsSlips_FromLocation] ON [dbo].[MpsSlips]([FromLocation]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_MpsSlips_ToLocation' AND object_id = OBJECT_ID(N'[dbo].[MpsSlips]'))
    CREATE INDEX [IX_MpsSlips_ToLocation] ON [dbo].[MpsSlips]([ToLocation]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_MpsSlips_Planner' AND object_id = OBJECT_ID(N'[dbo].[MpsSlips]'))
    CREATE INDEX [IX_MpsSlips_Planner] ON [dbo].[MpsSlips]([Planner]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_MpsSlips_ApprovalStatus' AND object_id = OBJECT_ID(N'[dbo].[MpsSlips]'))
    CREATE INDEX [IX_MpsSlips_ApprovalStatus] ON [dbo].[MpsSlips]([ApprovalStatus]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_ApprovalLogs_SlipId' AND object_id = OBJECT_ID(N'[dbo].[ApprovalLogs]'))
    CREATE INDEX [IX_ApprovalLogs_SlipId] ON [dbo].[ApprovalLogs]([SlipId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_ApprovalLogs_Timestamp' AND object_id = OBJECT_ID(N'[dbo].[ApprovalLogs]'))
    CREATE INDEX [IX_ApprovalLogs_Timestamp] ON [dbo].[ApprovalLogs]([Timestamp]);
GO

-- 6. Table: Users (Authentication & Role Access Control)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Users] (
        [Id] NVARCHAR(50) NOT NULL PRIMARY KEY,
        [Username] NVARCHAR(100) NOT NULL,
        [PasswordHash] NVARCHAR(MAX) NOT NULL,
        [DisplayName] NVARCHAR(100) NOT NULL,
        [Role] NVARCHAR(50) NOT NULL,
        [AssignedScope] NVARCHAR(100) NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE()
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Users_Username' AND object_id = OBJECT_ID(N'[dbo].[Users]'))
    CREATE UNIQUE INDEX [IX_Users_Username] ON [dbo].[Users]([Username]);
GO

-- Seed Default Users (Idempotent, password hash PBKDF2 valid)
IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'admin')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'admin', N'AQAAAAIAAYagAAAAEL4mju0oT3pFTXTsEeMT8bLdVqyW5F/UgzQUyqJoAJB41SjsvxGcJh3/2MUyo5HdIg==', N'Administrator', N'Admin', NULL, 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'uploader')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'uploader', N'AQAAAAIAAYagAAAAEDHzyefh1Lu6kKs6grVJIoItOCh/VtO7lXpE7xq84Wer6rJs70saqiCbkTJP2fq0NA==', N'Uploader', N'Uploader', NULL, 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'csline')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'csline', N'AQAAAAIAAYagAAAAEN5NKIZ84ivSHsSqGNrm/8wJtWtEQWjYSmZWI08TxgqS3F/pXIgksM1+i9iKSoc1aA==', N'H100 (CS LINE)', N'TO', N'H100 (CS LINE)', 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'h100')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'h100', N'AQAAAAIAAYagAAAAEHmFSVu3187sCtvkyXCZMoEE4jP8J8NHCjTdLHW2l5/tHZVEK1veNZv6LxUjw5rnrg==', N'H100 (CS LINE)', N'TO', N'H100 (CS LINE)', 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'a100')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'a100', N'AQAAAAIAAYagAAAAEHmFSVu3187sCtvkyXCZMoEE4jP8J8NHCjTdLHW2l5/tHZVEK1veNZv6LxUjw5rnrg==', N'A100 (MANUFACTURING 1)', N'TO', N'A100 (MANUFACTURING 1)', 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'b100')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'b100', N'AQAAAAIAAYagAAAAEHmFSVu3187sCtvkyXCZMoEE4jP8J8NHCjTdLHW2l5/tHZVEK1veNZv6LxUjw5rnrg==', N'B100 (MANUFACTURING 2)', N'TO', N'B100 (MANUFACTURING 2)', 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'e100')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'e100', N'AQAAAAIAAYagAAAAEHmFSVu3187sCtvkyXCZMoEE4jP8J8NHCjTdLHW2l5/tHZVEK1veNZv6LxUjw5rnrg==', N'E100 (PLASTIC ASSEMBLY)', N'TO', N'E100 (PLASTIC ASSEMBLY)', 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'f100')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'f100', N'AQAAAAIAAYagAAAAEHmFSVu3187sCtvkyXCZMoEE4jP8J8NHCjTdLHW2l5/tHZVEK1veNZv6LxUjw5rnrg==', N'F100 (CONTROL BOARD CS)', N'TO', N'F100 (CONTROL BOARD CS)', 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'g100')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'g100', N'AQAAAAIAAYagAAAAEHmFSVu3187sCtvkyXCZMoEE4jP8J8NHCjTdLHW2l5/tHZVEK1veNZv6LxUjw5rnrg==', N'G100 (CU LINE)', N'TO', N'G100 (CU LINE)', 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'hendro')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'hendro', N'AQAAAAIAAYagAAAAEBDMNTJiW+5hooXnuJwfBpSZ1lpAYqCp9MI7vo3tsBfLy1mT4daWGV07HXKQp7waXg==', N'R01 (HENDRO)', N'Planner', N'R01 (HENDRO)', 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'feby')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'feby', N'AQAAAAIAAYagAAAAEHmFSVu3187sCtvkyXCZMoEE4jP8J8NHCjTdLHW2l5/tHZVEK1veNZv6LxUjw5rnrg==', N'R02 (FEBY)', N'Planner', N'R02 (FEBY)', 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'angga')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'angga', N'AQAAAAIAAYagAAAAEHmFSVu3187sCtvkyXCZMoEE4jP8J8NHCjTdLHW2l5/tHZVEK1veNZv6LxUjw5rnrg==', N'R03 (ANGGA TRI WIBOWO)', N'Planner', N'R03 (ANGGA TRI WIBOWO)', 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'suroto')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'suroto', N'AQAAAAIAAYagAAAAEGiZbuNbStY7sAt6AP4wbhz1yt80a/zq1leljNQoG/qtXS3qpaOm9JDPi+F9oFDObA==', N'R04 (SUROTO)', N'Planner', N'R04 (SUROTO)', 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'masnuri')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'masnuri', N'AQAAAAIAAYagAAAAEIu6h5pl8Ks+bP0XADImQDI2e2uJVZhhaothO5qWjZwNBEsrYJzZnFO0RWm8MPBRgQ==', N'R06 (MASNURI)', N'Planner', N'R06 (MASNURI)', 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'hidayatullah')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'hidayatullah', N'AQAAAAIAAYagAAAAEBEFVBZcRlkPXfG28sMoaIRkAXQkI1toVGVkmw8hMfl+DNQxxCz42P9vrn8aIT8iOQ==', N'R07 (HIDAYATULLAH)', N'Planner', N'R07 (HIDAYATULLAH)', 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'taufiq')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'taufiq', N'AQAAAAIAAYagAAAAEDv4wTetsC0Id8yE+hFe6CBRIDqxKeiOdDhHj+gZvPx7EfGAWZiuJ3pv8kCsC5aZBg==', N'R08 (TAUFIQ ZAINURI)', N'Planner', N'R08 (TAUFIQ ZAINURI)', 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'ali')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'ali', N'AQAAAAIAAYagAAAAEMd3j/LHmU9GEIZdMropWXyPr5B7ZCNWXu0STB/C7aXeaL76qVweYGvCtc/DpdOTjQ==', N'R12 (ALI AKBAR)', N'Planner', N'R12 (ALI AKBAR)', 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'adang')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'adang', N'AQAAAAIAAYagAAAAEHmFSVu3187sCtvkyXCZMoEE4jP8J8NHCjTdLHW2l5/tHZVEK1veNZv6LxUjw5rnrg==', N'R13 (ADANG)', N'Planner', N'R13 (ADANG)', 1, GETDATE());
GO

-- ==========================================================
-- OPTIONAL: Grant IIS App Pool Access (For IIS Windows Auth)
-- Replace 'WebMPS' with your actual IIS Application Pool Name if different.
-- ==========================================================
/*
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = N'IIS APPPOOL\WebMPS')
    CREATE LOGIN [IIS APPPOOL\WebMPS] FROM WINDOWS;
GO

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = N'IIS APPPOOL\WebMPS')
    CREATE USER [IIS APPPOOL\WebMPS] FOR LOGIN [IIS APPPOOL\WebMPS];
GO

ALTER ROLE [db_owner] ADD MEMBER [IIS APPPOOL\WebMPS];
GO
*/
