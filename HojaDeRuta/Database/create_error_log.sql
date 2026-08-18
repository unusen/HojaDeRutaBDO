IF OBJECT_ID(N'dbo.ErrorLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ErrorLog
    (
        Id bigint IDENTITY(1,1) NOT NULL,
        IncidentId varchar(12) NOT NULL,
        OccurredAt datetime2(3) NOT NULL,
        ErrorCode varchar(80) NOT NULL,
        UserName nvarchar(256) NULL,
        HojaId nvarchar(128) NULL,
        OperationId varchar(64) NULL,
        Endpoint nvarchar(512) NOT NULL,
        UserMessage nvarchar(500) NOT NULL,
        ExceptionMessage nvarchar(4000) NOT NULL,
        Fingerprint varchar(64) NULL,
        ResolvedAt datetime2(3) NULL,
        CONSTRAINT PK_ErrorLog PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UX_ErrorLog_IncidentId UNIQUE (IncidentId)
    );
    CREATE INDEX IX_ErrorLog_OccurredAt ON dbo.ErrorLog (OccurredAt);
    CREATE INDEX IX_ErrorLog_HojaId ON dbo.ErrorLog (HojaId);
    CREATE INDEX IX_ErrorLog_OperationId ON dbo.ErrorLog (OperationId);
    CREATE INDEX IX_ErrorLog_ErrorCode ON dbo.ErrorLog (ErrorCode);
    CREATE UNIQUE INDEX UX_ErrorLog_Fingerprint_Open ON dbo.ErrorLog (Fingerprint) WHERE Fingerprint IS NOT NULL AND ResolvedAt IS NULL;
END
