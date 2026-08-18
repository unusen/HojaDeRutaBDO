CREATE OR ALTER PROCEDURE dbo.sp_sync_usuario_directorio
    @entra_object_id NVARCHAR(100),
    @mail NVARCHAR(150),
    @given_name NVARCHAR(150) = NULL,
    @surname NVARCHAR(150) = NULL,
    @area NVARCHAR(150) = NULL,
    @nivel INT = NULL,
    @detalle NVARCHAR(350) = NULL,
    @es_hdr BIT,
    @permitir_reasignacion_por_mail BIT = 0,
    @resultado_detalle NVARCHAR(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @cambios INT = 0;
    DECLARE @afectados INT;
    DECLARE @acciones NVARCHAR(500) = N'';
    DECLARE @estaba_inactivo BIT;
    SET @resultado_detalle = N'';

    BEGIN TRANSACTION;
    BEGIN TRY
        IF @es_hdr = 1 AND @permitir_reasignacion_por_mail = 0
           AND EXISTS (SELECT 1 FROM dbo.REVISORES WHERE mail = @mail AND NULLIF(entra_object_id, '') IS NOT NULL AND entra_object_id <> @entra_object_id)
        BEGIN
            ROLLBACK TRANSACTION;
            SET @resultado_detalle = N'Conflicto de identidad en REVISORES';
            RETURN -3;
        END

        IF @es_hdr = 1 AND @permitir_reasignacion_por_mail = 0
           AND EXISTS (SELECT 1 FROM dbo.SOCIOS WHERE mail = @mail AND NULLIF(entra_object_id, '') IS NOT NULL AND entra_object_id <> @entra_object_id)
        BEGIN
            ROLLBACK TRANSACTION;
            SET @resultado_detalle = N'Conflicto de identidad en SOCIOS';
            RETURN -3;
        END

        IF @es_hdr = 0
        BEGIN
            UPDATE dbo.REVISORES
            SET Hdr_Activo = 0
            WHERE Hdr_Activo = 1
              AND (entra_object_id = @entra_object_id OR (NULLIF(entra_object_id, '') IS NULL AND mail = @mail));
            SET @afectados = @@ROWCOUNT;
            SET @cambios += @afectados;
            IF @afectados > 0 SET @acciones += N'RevisorInactivadoPorNoPertenecerGrupoHDR;';

            UPDATE dbo.SOCIOS
            SET Hdr_Activo = 0
            WHERE Hdr_Activo = 1
              AND (entra_object_id = @entra_object_id OR (NULLIF(entra_object_id, '') IS NULL AND mail = @mail));
            SET @afectados = @@ROWCOUNT;
            SET @cambios += @afectados;
            IF @afectados > 0 SET @acciones += N'SocioInactivadoPorNoPertenecerGrupoHDR;';

            COMMIT TRANSACTION;
            SET @resultado_detalle = @acciones;
            RETURN @cambios;
        END

        DECLARE @subarea NVARCHAR(150);
        SELECT TOP (1) @subarea = r.subarea
        FROM dbo.REVISORES r
        WHERE (r.entra_object_id = @entra_object_id
           OR (@permitir_reasignacion_por_mail = 1 AND r.mail = @mail)
           OR (@permitir_reasignacion_por_mail = 0 AND NULLIF(r.entra_object_id, '') IS NULL AND r.mail = @mail))
          AND EXISTS (SELECT 1 FROM dbo.SUBAREA sa WHERE sa.subarea = r.subarea AND sa.sector = @area)
        ORDER BY r.subarea;

        IF @subarea IS NULL
        BEGIN
            SELECT TOP (1) @subarea = sa.subarea
            FROM dbo.SUBAREA sa
            WHERE sa.sector = @area
            ORDER BY sa.subarea;
        END

        IF EXISTS (SELECT 1 FROM dbo.REVISORES WHERE entra_object_id = @entra_object_id OR (@permitir_reasignacion_por_mail = 1 AND mail = @mail) OR (@permitir_reasignacion_por_mail = 0 AND NULLIF(entra_object_id, '') IS NULL AND mail = @mail))
        BEGIN
            SET @estaba_inactivo = CASE WHEN EXISTS (SELECT 1 FROM dbo.REVISORES WHERE Hdr_Activo = 0 AND (entra_object_id = @entra_object_id OR (@permitir_reasignacion_por_mail = 1 AND mail = @mail) OR (@permitir_reasignacion_por_mail = 0 AND NULLIF(entra_object_id, '') IS NULL AND mail = @mail))) THEN 1 ELSE 0 END;
            UPDATE dbo.REVISORES
            SET empleado = @mail, mail = @mail, detalle = @detalle, cargo = @nivel, area = @area, subarea = @subarea, entra_object_id = @entra_object_id, Hdr_Activo = 1
            WHERE (entra_object_id = @entra_object_id OR (@permitir_reasignacion_por_mail = 1 AND mail = @mail) OR (@permitir_reasignacion_por_mail = 0 AND NULLIF(entra_object_id, '') IS NULL AND mail = @mail))
              AND (ISNULL(empleado, '') <> @mail OR ISNULL(detalle, '') <> @detalle OR ISNULL(cargo, -1) <> @nivel OR ISNULL(area, '') <> @area OR ISNULL(subarea, '') <> ISNULL(@subarea, '') OR ISNULL(entra_object_id, '') <> @entra_object_id OR Hdr_Activo <> 1);
            SET @afectados = @@ROWCOUNT;
            SET @cambios += @afectados;
            IF @afectados > 0 SET @acciones += CASE WHEN @estaba_inactivo = 1 THEN N'RevisorReactivado;' ELSE N'RevisorActualizado;' END;
        END
        ELSE
        BEGIN
            INSERT INTO dbo.REVISORES (empleado, detalle, mail, cargo, area, subarea, entra_object_id, Hdr_Activo)
            VALUES (@mail, @detalle, @mail, @nivel, @area, @subarea, @entra_object_id, 1);
            SET @cambios += 1;
            SET @acciones += N'RevisorCreado;';
        END

        IF @nivel NOT IN (9, 10)
        BEGIN
            UPDATE dbo.SOCIOS
            SET Hdr_Activo = 0
            WHERE Hdr_Activo = 1
              AND (entra_object_id = @entra_object_id OR (@permitir_reasignacion_por_mail = 1 AND mail = @mail) OR (@permitir_reasignacion_por_mail = 0 AND NULLIF(entra_object_id, '') IS NULL AND mail = @mail));
            SET @afectados = @@ROWCOUNT;
            SET @cambios += @afectados;
            IF @afectados > 0 SET @acciones += N'SocioInactivadoPorCambioDeNivel;';
            COMMIT TRANSACTION;
            SET @resultado_detalle = @acciones;
            RETURN @cambios;
        END

        IF EXISTS (SELECT 1 FROM dbo.SOCIOS WHERE entra_object_id = @entra_object_id OR (@permitir_reasignacion_por_mail = 1 AND mail = @mail) OR (@permitir_reasignacion_por_mail = 0 AND NULLIF(entra_object_id, '') IS NULL AND mail = @mail))
        BEGIN
            SET @estaba_inactivo = CASE WHEN EXISTS (SELECT 1 FROM dbo.SOCIOS WHERE Hdr_Activo = 0 AND (entra_object_id = @entra_object_id OR (@permitir_reasignacion_por_mail = 1 AND mail = @mail) OR (@permitir_reasignacion_por_mail = 0 AND NULLIF(entra_object_id, '') IS NULL AND mail = @mail))) THEN 1 ELSE 0 END;
            UPDATE dbo.SOCIOS
            SET mail = @mail, detalle = @detalle, liderDeArea = CASE WHEN @nivel = 10 THEN 1 ELSE 0 END, entra_object_id = @entra_object_id, Hdr_Activo = 1
            WHERE (entra_object_id = @entra_object_id OR (@permitir_reasignacion_por_mail = 1 AND mail = @mail) OR (@permitir_reasignacion_por_mail = 0 AND NULLIF(entra_object_id, '') IS NULL AND mail = @mail))
              AND (ISNULL(mail, '') <> @mail OR ISNULL(detalle, '') <> @detalle OR ISNULL(liderDeArea, 0) <> CASE WHEN @nivel = 10 THEN 1 ELSE 0 END OR ISNULL(entra_object_id, '') <> @entra_object_id OR Hdr_Activo <> 1);
            SET @afectados = @@ROWCOUNT;
            SET @cambios += @afectados;
            IF @afectados > 0 SET @acciones += CASE WHEN @estaba_inactivo = 1 THEN N'SocioReactivado;' ELSE N'SocioActualizado;' END;
        END
        ELSE
        BEGIN
            DECLARE @codigo NVARCHAR(50) = UPPER(LEFT(@surname, 1) + LEFT(@given_name, 1));
            IF EXISTS (SELECT 1 FROM dbo.SOCIOS WHERE socio = @codigo) SET @codigo = UPPER(LEFT(@given_name, 1) + LEFT(@surname, 1));
            IF EXISTS (SELECT 1 FROM dbo.SOCIOS WHERE socio = @codigo) SET @codigo = UPPER(LEFT(@surname, 2));
            IF EXISTS (SELECT 1 FROM dbo.SOCIOS WHERE socio = @codigo) SET @codigo = UPPER(LEFT(@given_name, 2));
            DECLARE @baseCodigo NVARCHAR(50) = @codigo;
            DECLARE @sufijo INT = 2;
            WHILE EXISTS (SELECT 1 FROM dbo.SOCIOS WHERE socio = @codigo)
            BEGIN
                SET @codigo = @baseCodigo + CONVERT(NVARCHAR(10), @sufijo);
                SET @sufijo += 1;
            END
            INSERT INTO dbo.SOCIOS (socio, detalle, mail, sector, liderDeArea, entra_object_id, Hdr_Activo)
            VALUES (@codigo, @detalle, @mail, @area, CASE WHEN @nivel = 10 THEN 1 ELSE 0 END, @entra_object_id, 1);
            SET @cambios += 1;
            SET @acciones += N'SocioCreado;';
        END

        COMMIT TRANSACTION;
        SET @resultado_detalle = @acciones;
        RETURN @cambios;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
