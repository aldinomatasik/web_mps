-- ==========================================================
-- WebMPS Migration Script: Add Users Table & Default Accounts
-- Execute this query in SSMS on the target database server
-- Passwords:
--   admin                -> admin123
--   uploader             -> uploader123
--   Semua Staff (TO & Planner) -> 123456
-- ==========================================================

USE [WebMPS];
GO

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
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username] ON [dbo].[Users]([Username]);
END
GO

-- 1. Admin & Uploader
IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'admin')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'admin', N'AQAAAAIAAYagAAAAEL4mju0oT3pFTXTsEeMT8bLdVqyW5F/UgzQUyqJoAJB41SjsvxGcJh3/2MUyo5HdIg==', N'Administrator', N'Admin', NULL, 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'uploader')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'uploader', N'AQAAAAIAAYagAAAAEDHzyefh1Lu6kKs6grVJIoItOCh/VtO7lXpE7xq84Wer6rJs70saqiCbkTJP2fq0NA==', N'Uploader', N'Uploader', NULL, 1, GETDATE());

-- 2. Akun 6 Line / TO
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

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'csline')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'csline', N'AQAAAAIAAYagAAAAEN5NKIZ84ivSHsSqGNrm/8wJtWtEQWjYSmZWI08TxgqS3F/pXIgksM1+i9iKSoc1aA==', N'H100 (CS LINE)', N'TO', N'H100 (CS LINE)', 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE LOWER([Username]) = N'h100')
    INSERT INTO [dbo].[Users] ([Id], [Username], [PasswordHash], [DisplayName], [Role], [AssignedScope], [IsActive], [CreatedAt])
    VALUES (REPLACE(NEWID(), '-', ''), N'h100', N'AQAAAAIAAYagAAAAEHmFSVu3187sCtvkyXCZMoEE4jP8J8NHCjTdLHW2l5/tHZVEK1veNZv6LxUjw5rnrg==', N'H100 (CS LINE)', N'TO', N'H100 (CS LINE)', 1, GETDATE());

-- 3. Akun 9 Planner
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

-- Verify inserted users
SELECT [Username], [DisplayName], [Role], [AssignedScope], [IsActive] FROM [dbo].[Users] ORDER BY [Role], [Username];
GO
