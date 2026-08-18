CREATE OR ALTER PROCEDURE dbo.sp_get_hojas_by_nivel_base
    @Nivel INT = NULL,
    @Sector NVARCHAR(50) = NULL,
    @Sectores NVARCHAR(MAX) = NULL,
    @Usuario NVARCHAR(50) = NULL,
    @Id NVARCHAR(50) = NULL,
    @Pendientes BIT
AS
BEGIN
    SET NOCOUNT ON;

    IF @Id IS NOT NULL
    BEGIN
        SELECT
            h.id, h.usuario, h.equipo, h.cliente, h.sector, h.subarea,
            h.numeracion, h.descripcion, h.socio_firmante, h.sindica,
            h.contrato, h.preparo, h.reviso, h.revisogte, h.revisosocio,
            h.rutapapeles, h.rutadoc, h.observaciones, h.preparo_fecha,
            h.reviso_fecha, h.revisogte_fecha, h.revisosocio_fecha,
            h.fecha_modif, h.hora_modif, h.estado, h.mailasociado,
            h.generico, h.revisoengagement, h.nivel_doc, h.manejador,
            h.fecha_limite, h.lugar_firma, h.manejador_final,
            h.revisoengagement_fecha, h.Fecha, h.fecha_cierre, h.adjuntos,
            h.archivo_temp, h.archivo_hash,
            c.RazonSocial AS ClienteName
        FROM HOJAS h
        LEFT JOIN Clientes_Creatio c
            ON c.ID = h.cliente
        WHERE h.id = @Id;

        RETURN;
    END;

    IF @Pendientes = 1
    BEGIN
        IF @Nivel = 11
        BEGIN
            SELECT
                h.id, h.usuario, h.equipo, h.cliente, h.sector, h.subarea,
                h.numeracion, h.descripcion, h.socio_firmante, h.sindica,
                h.contrato, h.preparo, h.reviso, h.revisogte, h.revisosocio,
                h.rutapapeles, h.rutadoc, h.observaciones, h.preparo_fecha,
                h.reviso_fecha, h.revisogte_fecha, h.revisosocio_fecha,
                h.fecha_modif, h.hora_modif, h.estado, h.mailasociado,
                h.generico, h.revisoengagement, h.nivel_doc, h.manejador,
                h.fecha_limite, h.lugar_firma, h.manejador_final,
                h.revisoengagement_fecha, h.Fecha, h.fecha_cierre, h.adjuntos,
                h.archivo_temp, h.archivo_hash,
                c.RazonSocial AS ClienteName
            FROM HOJAS h
            LEFT JOIN Clientes_Creatio c
                ON c.ID = h.cliente
            WHERE EXISTS
            (
                SELECT 1
                FROM Hoja_Estado he
                WHERE he.HojaId = h.id
                  AND he.Estado = 0
            );

            RETURN;
        END;

        IF @Nivel = 10
        BEGIN
            SELECT
                h.id, h.usuario, h.equipo, h.cliente, h.sector, h.subarea,
                h.numeracion, h.descripcion, h.socio_firmante, h.sindica,
                h.contrato, h.preparo, h.reviso, h.revisogte, h.revisosocio,
                h.rutapapeles, h.rutadoc, h.observaciones, h.preparo_fecha,
                h.reviso_fecha, h.revisogte_fecha, h.revisosocio_fecha,
                h.fecha_modif, h.hora_modif, h.estado, h.mailasociado,
                h.generico, h.revisoengagement, h.nivel_doc, h.manejador,
                h.fecha_limite, h.lugar_firma, h.manejador_final,
                h.revisoengagement_fecha, h.Fecha, h.fecha_cierre, h.adjuntos,
                h.archivo_temp, h.archivo_hash,
                c.RazonSocial AS ClienteName
            FROM HOJAS h
            LEFT JOIN Clientes_Creatio c
                ON c.ID = h.cliente
            WHERE
                (
                    (
                        h.sector = @Sector
                        OR EXISTS
                        (
                            SELECT 1
                            FROM STRING_SPLIT(ISNULL(@Sectores, N''), N',') AS sectores
                            WHERE LTRIM(RTRIM(sectores.value)) = h.sector
                        )
                    )
                    OR @Usuario IN
                    (
                        h.preparo,
                        h.reviso,
                        h.revisogte,
                        h.revisosocio,
                        h.revisoengagement,
                        h.manejador,
                        h.manejador_final
                    )
                )
                AND EXISTS
                (
                    SELECT 1
                    FROM Hoja_Estado he
                    WHERE he.HojaId = h.id
                      AND he.Estado = 0
                );

            RETURN;
        END;

        IF @Nivel <= 9
        BEGIN
            SELECT
                h.id, h.usuario, h.equipo, h.cliente, h.sector, h.subarea,
                h.numeracion, h.descripcion, h.socio_firmante, h.sindica,
                h.contrato, h.preparo, h.reviso, h.revisogte, h.revisosocio,
                h.rutapapeles, h.rutadoc, h.observaciones, h.preparo_fecha,
                h.reviso_fecha, h.revisogte_fecha, h.revisosocio_fecha,
                h.fecha_modif, h.hora_modif, h.estado, h.mailasociado,
                h.generico, h.revisoengagement, h.nivel_doc, h.manejador,
                h.fecha_limite, h.lugar_firma, h.manejador_final,
                h.revisoengagement_fecha, h.Fecha, h.fecha_cierre, h.adjuntos,
                h.archivo_temp, h.archivo_hash,
                c.RazonSocial AS ClienteName
            FROM HOJAS h
            LEFT JOIN Clientes_Creatio c
                ON c.ID = h.cliente
            WHERE
                @Usuario IN
                (
                    h.preparo,
                    h.reviso,
                    h.revisogte,
                    h.revisosocio,
                    h.revisoengagement,
                    h.manejador,
                    h.manejador_final
                )
                AND EXISTS
                (
                    SELECT 1
                    FROM Hoja_Estado he
                    WHERE he.HojaId = h.id
                      AND he.Revisor = @Usuario
                      AND he.Estado = 0
                );

            RETURN;
        END;

        RETURN;
    END;

    IF @Nivel = 11
    BEGIN
        SELECT
            h.id, h.usuario, h.equipo, h.cliente, h.sector, h.subarea,
            h.numeracion, h.descripcion, h.socio_firmante, h.sindica,
            h.contrato, h.preparo, h.reviso, h.revisogte, h.revisosocio,
            h.rutapapeles, h.rutadoc, h.observaciones, h.preparo_fecha,
            h.reviso_fecha, h.revisogte_fecha, h.revisosocio_fecha,
            h.fecha_modif, h.hora_modif, h.estado, h.mailasociado,
            h.generico, h.revisoengagement, h.nivel_doc, h.manejador,
            h.fecha_limite, h.lugar_firma, h.manejador_final,
            h.revisoengagement_fecha, h.Fecha, h.fecha_cierre, h.adjuntos,
            h.archivo_temp, h.archivo_hash,
            c.RazonSocial AS ClienteName
        FROM HOJAS h
        LEFT JOIN Clientes_Creatio c
            ON c.ID = h.cliente;
    END
    ELSE IF @Nivel = 10
    BEGIN
        SELECT
            h.id, h.usuario, h.equipo, h.cliente, h.sector, h.subarea,
            h.numeracion, h.descripcion, h.socio_firmante, h.sindica,
            h.contrato, h.preparo, h.reviso, h.revisogte, h.revisosocio,
            h.rutapapeles, h.rutadoc, h.observaciones, h.preparo_fecha,
            h.reviso_fecha, h.revisogte_fecha, h.revisosocio_fecha,
            h.fecha_modif, h.hora_modif, h.estado, h.mailasociado,
            h.generico, h.revisoengagement, h.nivel_doc, h.manejador,
            h.fecha_limite, h.lugar_firma, h.manejador_final,
            h.revisoengagement_fecha, h.Fecha, h.fecha_cierre, h.adjuntos,
            h.archivo_temp, h.archivo_hash,
            c.RazonSocial AS ClienteName
        FROM HOJAS h
        LEFT JOIN Clientes_Creatio c
            ON c.ID = h.cliente
        WHERE
            (
                h.sector = @Sector
                OR EXISTS
                (
                    SELECT 1
                    FROM STRING_SPLIT(ISNULL(@Sectores, N''), N',') AS sectores
                    WHERE LTRIM(RTRIM(sectores.value)) = h.sector
                )
            )
            OR @Usuario IN
            (
                h.preparo,
                h.reviso,
                h.revisogte,
                h.revisosocio,
                h.revisoengagement,
                h.manejador,
                h.manejador_final
            );
    END
    ELSE IF @Nivel <= 9
    BEGIN
        SELECT
            h.id, h.usuario, h.equipo, h.cliente, h.sector, h.subarea,
            h.numeracion, h.descripcion, h.socio_firmante, h.sindica,
            h.contrato, h.preparo, h.reviso, h.revisogte, h.revisosocio,
            h.rutapapeles, h.rutadoc, h.observaciones, h.preparo_fecha,
            h.reviso_fecha, h.revisogte_fecha, h.revisosocio_fecha,
            h.fecha_modif, h.hora_modif, h.estado, h.mailasociado,
            h.generico, h.revisoengagement, h.nivel_doc, h.manejador,
            h.fecha_limite, h.lugar_firma, h.manejador_final,
            h.revisoengagement_fecha, h.Fecha, h.fecha_cierre, h.adjuntos,
            h.archivo_temp, h.archivo_hash,
            c.RazonSocial AS ClienteName
        FROM HOJAS h
        LEFT JOIN Clientes_Creatio c
            ON c.ID = h.cliente
        WHERE @Usuario IN
        (
            h.preparo,
            h.reviso,
            h.revisogte,
            h.revisosocio,
            h.revisoengagement,
            h.manejador,
            h.manejador_final
        );
    END
END;
