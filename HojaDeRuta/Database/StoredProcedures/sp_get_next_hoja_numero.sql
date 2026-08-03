CREATE PROCEDURE dbo.sp_get_next_hoja_numero
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @NextNumero INT;

    BEGIN TRAN;

    BEGIN TRY
        UPDATE dbo.HojaNumerador WITH (UPDLOCK, HOLDLOCK)
        SET @NextNumero = UltimoNumero = UltimoNumero + 1
        WHERE Nombre = 'HOJAS';

        IF @NextNumero IS NULL OR @NextNumero <= 0
        BEGIN
            THROW 50001, 'No se pudo reservar el siguiente numero de hoja.', 1;
        END

        COMMIT TRAN;

        RETURN @NextNumero;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRAN;

        THROW;
    END CATCH
END;