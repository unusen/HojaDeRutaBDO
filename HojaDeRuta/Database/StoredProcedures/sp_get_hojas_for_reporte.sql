CREATE PROCEDURE [dbo].[sp_get_hojas_for_reporte]
    @SocioFirmante NVARCHAR(100) = NULL,
    @FechaDesde DATE = NULL,
    @FechaHasta DATE = NULL,
    @ColumnasSeleccionadas NVARCHAR(MAX) = NULL,
    @Auditoria BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SQL NVARCHAR(MAX);
    DECLARE @ColumnasIniciales NVARCHAR(MAX); -- Usaremos esta variable en lugar de @ColumnasFinales
    DECLARE @ColumnasAuditoria NVARCHAR(MAX) = N'';
    DECLARE @JoinAuditoria NVARCHAR(MAX) = N'';
    DECLARE @Filtros NVARCHAR(MAX) = N' 1=1 ';

    -- 1. DETERMINAR LAS COLUMNAS BASE

    -- Si el usuario seleccionó columnas, usamos esas.
    IF ISNULL(NULLIF(LTRIM(RTRIM(@ColumnasSeleccionadas)), ''), '') <> ''
    BEGIN
        SET @ColumnasIniciales = LTRIM(RTRIM(@ColumnasSeleccionadas));
    END
    ELSE
    BEGIN
        -- Si no seleccionó columnas, usamos H.* para obtener TODAS las columnas de HOJAS,
        -- pero evitamos seleccionar todas las columnas de AUDITORIA con el simple '*'
        SET @ColumnasIniciales = N'H.*';
    END

    -- 2. LÓGICA DE AUDITORÍA
    IF @Auditoria = 1
    BEGIN
        -- ... (El cálculo @FactorMonedaSQL se mantiene igual) ...
        DECLARE @FactorMonedaSQL NVARCHAR(MAX) = N'
            CASE
                WHEN A.TipoNumeracion = ''MILES'' THEN 1000.0
                WHEN A.TipoNumeracion = ''MILLONES'' THEN 1000000.0
                ELSE 1.0
            END';

        SET @ColumnasAuditoria = N',
            -- Las columnas de auditoría calculadas (con sus alias únicos) se añaden después de la base (H.*)
            (A.Activo * ' + @FactorMonedaSQL + N') as Activo,
            (A.Pasivo * ' + @FactorMonedaSQL + N') as Pasivo,
            (A.PatrimonioNeto * ' + @FactorMonedaSQL + N') as PatrimonioNeto,
            A.Moneda,
            A.TipoNumeracion,
            (A.Resultado * ' + @FactorMonedaSQL + N') as Resultado,
            (A.TotalIngresos * ' + @FactorMonedaSQL + N') as TotalIngresos,
            (A.TotalOtrosIngresos * ' + @FactorMonedaSQL + N') as TotalOtrosIngresos
        ';

        SET @JoinAuditoria = N'
            LEFT JOIN Auditorias AS A ON H.Id = A.HojaId ';
    END

    -- 3. Construcción de Filtros (Se omite por brevedad, se mantiene igual)
    IF @SocioFirmante IS NOT NULL
       AND LTRIM(RTRIM(@SocioFirmante)) <> ''
    BEGIN
        SET @Filtros = @Filtros + N' AND H.revisosocio = @SocioFirmante ';
    END

    IF @FechaDesde IS NOT NULL
    BEGIN
        SET @Filtros = @Filtros + N' AND H.fecha >= @FechaDesde ';
    END

    IF @FechaHasta IS NOT NULL
    BEGIN
        SET @Filtros = @Filtros + N' AND H.fecha <= @FechaHasta ';
    END

    -- 4. Construir la sentencia SQL completa
    SET @SQL = N'
        SELECT ' + @ColumnasIniciales + @ColumnasAuditoria + N'
        FROM HOJAS AS H ' + @JoinAuditoria + N'
        WHERE ' + @Filtros + N'
        ORDER BY H.numeracion DESC;';

    -- 5. Ejecutar
    EXEC sp_executesql
        @SQL,
        N'@SocioFirmante NVARCHAR(100), @FechaDesde DATE, @FechaHasta DATE',
        @SocioFirmante = @SocioFirmante,
        @FechaDesde = @FechaDesde,
        @FechaHasta = @FechaHasta;
END