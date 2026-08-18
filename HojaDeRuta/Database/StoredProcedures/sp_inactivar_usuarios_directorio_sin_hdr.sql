CREATE OR ALTER PROCEDURE dbo.sp_inactivar_usuarios_directorio_sin_hdr
    @usuarios_hdr dbo.HDR_DirectoryReconciliationIdentity READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @cambios INT = 0;
    BEGIN TRANSACTION;
    BEGIN TRY
        UPDATE r SET Hdr_Activo = 0
        FROM dbo.REVISORES r
        WHERE r.Hdr_Activo = 1
          AND NOT EXISTS (SELECT 1 FROM @usuarios_hdr u WHERE u.EntraObjectId = r.entra_object_id OR (NULLIF(r.entra_object_id, '') IS NULL AND u.AllowMailFallback = 1 AND u.Mail = r.mail));
        SET @cambios += @@ROWCOUNT;
        UPDATE s SET Hdr_Activo = 0
        FROM dbo.SOCIOS s
        WHERE s.Hdr_Activo = 1
          AND NOT EXISTS (SELECT 1 FROM @usuarios_hdr u WHERE u.EntraObjectId = s.entra_object_id OR (NULLIF(s.entra_object_id, '') IS NULL AND u.AllowMailFallback = 1 AND u.Mail = s.mail));
        SET @cambios += @@ROWCOUNT;
        COMMIT TRANSACTION;
        RETURN @cambios;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
