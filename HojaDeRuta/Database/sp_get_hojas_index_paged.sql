CREATE OR ALTER PROCEDURE dbo.sp_get_hojas_index_paged
    @Nivel INT,
    @Sector NVARCHAR(50),
    @Usuario NVARCHAR(100),
    @Pendientes BIT,
    @Numero NVARCHAR(50) = NULL,
    @Cliente NVARCHAR(255) = NULL,
    @Estado INT = NULL,
    @SectorFiltro NVARCHAR(50) = NULL,
    @Socio NVARCHAR(255) = NULL,
    @FechaDesde DATE = NULL,
    @FechaHasta DATE = NULL,
    @SortField NVARCHAR(50) = N'Numero',
    @SortDirection NVARCHAR(4) = N'asc',
    @PageNumber INT = 1,
    @PageSize INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PendientesInt INT;

    IF (@PageNumber < 1) SET @PageNumber = 1;
    IF (@PageSize < 1) SET @PageSize = 10;
    SET @PendientesInt = CASE WHEN @Pendientes = 1 THEN 1 ELSE 0 END;

    IF OBJECT_ID('tempdb..#BaseHojas') IS NOT NULL
        DROP TABLE #BaseHojas;

    CREATE TABLE #BaseHojas
    (
        id NVARCHAR(8) NULL,
        usuario NVARCHAR(255) NULL,
        equipo NVARCHAR(255) NULL,
        cliente INT NULL,
        sector NVARCHAR(4) NULL,
        subarea NVARCHAR(6) NULL,
        numeracion NVARCHAR(4) NULL,
        descripcion NVARCHAR(255) NULL,
        socio_firmante NVARCHAR(255) NULL,
        sindica NVARCHAR(255) NULL,
        contrato NVARCHAR(255) NULL,
        preparo NVARCHAR(255) NULL,
        reviso NVARCHAR(255) NULL,
        revisogte NVARCHAR(255) NULL,
        revisosocio NVARCHAR(255) NULL,
        rutapapeles NVARCHAR(255) NULL,
        rutadoc NVARCHAR(255) NULL,
        observaciones NVARCHAR(255) NULL,
        preparo_fecha NVARCHAR(255) NULL,
        reviso_fecha NVARCHAR(255) NULL,
        revisogte_fecha NVARCHAR(255) NULL,
        revisosocio_fecha NVARCHAR(255) NULL,
        fecha_modif NVARCHAR(10) NULL,
        hora_modif NVARCHAR(10) NULL,
        estado INT NULL,
        mailasociado NVARCHAR(255) NULL,
        generico NVARCHAR(255) NULL,
        revisoengagement NVARCHAR(255) NULL,
        nivel_doc INT NULL,
        manejador NVARCHAR(255) NULL,
        fecha_limite NVARCHAR(10) NULL,
        lugar_firma NVARCHAR(255) NULL,
        manejador_final NVARCHAR(255) NULL,
        revisoengagement_fecha NVARCHAR(255) NULL,
        Fecha DATE NULL,
        fecha_cierre DATE NULL,
        adjuntos NVARCHAR(MAX) NULL,
        archivo_temp NVARCHAR(MAX) NULL,
        archivo_hash NVARCHAR(MAX) NULL,
        ClienteName NVARCHAR(255) NULL
    );

    INSERT INTO #BaseHojas
    EXEC dbo.sp_get_hojas_by_nivel
        @Nivel = @Nivel,
        @Sector = @Sector,
        @Usuario = @Usuario,
        @Id = NULL,
        @Pendientes = @PendientesInt;

    ;WITH Base AS
    (
        SELECT
            h.id AS Id,
            h.cliente AS Cliente,
            h.ClienteName,
            h.sector AS Sector,
            h.subarea AS Subarea,
            h.numeracion AS Numero,
            h.generico AS NombreGenerico,
            h.descripcion AS Descripcion,
            h.Fecha AS FechaDocumento,
            h.revisosocio AS SocioFirmante,
            sf.Detalle AS SocioFirmanteDetalle,
            h.sindica AS Sindico,
            h.contrato AS ContratoPlataforma,
            h.preparo AS Preparo,
            h.preparo_fecha AS PreparoFecha,
            h.reviso AS Reviso,
            h.reviso_fecha AS RevisoFecha,
            h.revisogte AS RevisionGerente,
            h.revisogte_fecha AS RevisionGerenteFecha,
            h.revisoengagement AS EngagementPartner,
            h.revisoengagement_fecha AS EngagementPartnerFecha,
            h.revisosocio_fecha AS SocioFirmanteFecha,
            h.fecha_limite AS FechaLimite,
            h.fecha_cierre AS FechaDeCierre,
            h.manejador_final AS GestorFinal,
            h.manejador AS Manejador,
            h.lugar_firma AS LugarFirma,
            h.rutadoc AS RutaDoc,
            h.rutapapeles AS RutaPapeles,
            h.adjuntos AS Adjuntos,
            h.archivo_temp AS ArchivoTemp,
            h.archivo_hash AS ArchivoHash,
            h.observaciones AS Observaciones,
            h.estado AS Estado
        FROM #BaseHojas h
        LEFT JOIN dbo.SOCIOS sf ON sf.mail = h.revisosocio
        WHERE (@Numero IS NULL OR h.numeracion LIKE '%' + @Numero + '%')
          AND (@Cliente IS NULL OR h.ClienteName LIKE '%' + @Cliente + '%')
          AND (@Estado IS NULL OR h.estado = @Estado)
          AND (@SectorFiltro IS NULL OR h.sector LIKE '%' + @SectorFiltro + '%')
          AND (@Socio IS NULL OR sf.Detalle LIKE '%' + @Socio + '%')
          AND (@FechaDesde IS NULL OR h.Fecha >= @FechaDesde)
          AND (@FechaHasta IS NULL OR h.Fecha <= @FechaHasta)
    ),
    Ordered AS
    (
        SELECT
            *,
            COUNT(1) OVER() AS TotalItems,
            ROW_NUMBER() OVER
            (
                ORDER BY
                    CASE WHEN @SortField = N'Numero' AND @SortDirection = N'asc' THEN Numero END ASC,
                    CASE WHEN @SortField = N'Numero' AND @SortDirection = N'desc' THEN Numero END DESC,
                    CASE WHEN @SortField = N'ClienteName' AND @SortDirection = N'asc' THEN ClienteName END ASC,
                    CASE WHEN @SortField = N'ClienteName' AND @SortDirection = N'desc' THEN ClienteName END DESC,
                    CASE WHEN @SortField = N'NombreGenerico' AND @SortDirection = N'asc' THEN NombreGenerico END ASC,
                    CASE WHEN @SortField = N'NombreGenerico' AND @SortDirection = N'desc' THEN NombreGenerico END DESC,
                    CASE WHEN @SortField = N'Sector' AND @SortDirection = N'asc' THEN Sector END ASC,
                    CASE WHEN @SortField = N'Sector' AND @SortDirection = N'desc' THEN Sector END DESC,
                    CASE WHEN @SortField = N'SocioFirmanteDetalle' AND @SortDirection = N'asc' THEN SocioFirmanteDetalle END ASC,
                    CASE WHEN @SortField = N'SocioFirmanteDetalle' AND @SortDirection = N'desc' THEN SocioFirmanteDetalle END DESC,
                    CASE WHEN @SortField = N'FechaDocumento' AND @SortDirection = N'asc' THEN FechaDocumento END ASC,
                    CASE WHEN @SortField = N'FechaDocumento' AND @SortDirection = N'desc' THEN FechaDocumento END DESC,
                    CASE WHEN @SortField = N'Estado' AND @SortDirection = N'asc' THEN Estado END ASC,
                    CASE WHEN @SortField = N'Estado' AND @SortDirection = N'desc' THEN Estado END DESC,
                    Numero ASC
            ) AS RowNumber
        FROM Base
    )
    SELECT
        Id,
        Cliente,
        ClienteName,
        Sector,
        Subarea,
        Numero,
        NombreGenerico,
        Descripcion,
        FechaDocumento,
        SocioFirmante,
        SocioFirmanteDetalle,
        Sindico,
        ContratoPlataforma,
        Preparo,
        PreparoFecha,
        Reviso,
        RevisoFecha,
        RevisionGerente,
        RevisionGerenteFecha,
        EngagementPartner,
        EngagementPartnerFecha,
        SocioFirmanteFecha,
        FechaLimite,
        FechaDeCierre,
        GestorFinal,
        Manejador,
        LugarFirma,
        RutaDoc,
        RutaPapeles,
        Adjuntos,
        ArchivoTemp,
        ArchivoHash,
        Observaciones,
        Estado,
        TotalItems,
        @PageNumber AS PageNumber,
        @PageSize AS PageSize
    FROM Ordered
    WHERE RowNumber BETWEEN ((@PageNumber - 1) * @PageSize) + 1
                        AND (@PageNumber * @PageSize)
    ORDER BY RowNumber;
END
GO
