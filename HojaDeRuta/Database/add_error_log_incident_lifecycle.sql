IF COL_LENGTH(N'dbo.ErrorLog', N'Fingerprint') IS NULL ALTER TABLE dbo.ErrorLog ADD Fingerprint varchar(64) NULL;
IF COL_LENGTH(N'dbo.ErrorLog', N'ResolvedAt') IS NULL ALTER TABLE dbo.ErrorLog ADD ResolvedAt datetime2(3) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ErrorLog') AND name = N'UX_ErrorLog_Fingerprint_Open') CREATE UNIQUE INDEX UX_ErrorLog_Fingerprint_Open ON dbo.ErrorLog (Fingerprint) WHERE Fingerprint IS NOT NULL AND ResolvedAt IS NULL;
