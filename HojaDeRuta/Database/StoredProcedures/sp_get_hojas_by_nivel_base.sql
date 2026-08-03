CREATE PROCEDURE dbo.sp_get_hojas_by_nivel_base
    @Nivel INT = NULL,
    @Sector NVARCHAR(50) = NULL,
    @Usuario NVARCHAR(50) = NULL,
    @Id NVARCHAR(50) = NULL,
    @Pendientes BIT
AS
BEGIN
    SET NOCOUNT ON;

    IF @Id IS NOT NULL
    BEGIN
        SELECT
            h.*,
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
                h.*,
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
                h.*,
                c.RazonSocial AS ClienteName
            FROM HOJAS h
            LEFT JOIN Clientes_Creatio c
                ON c.ID = h.cliente
            WHERE
                (
                    h.sector = @Sector
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
                h.*,
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
            h.*,
            c.RazonSocial AS ClienteName
        FROM HOJAS h
        LEFT JOIN Clientes_Creatio c
            ON c.ID = h.cliente;
    END
    ELSE IF @Nivel = 10
    BEGIN
        SELECT
            h.*,
            c.RazonSocial AS ClienteName
        FROM HOJAS h
        LEFT JOIN Clientes_Creatio c
            ON c.ID = h.cliente
        WHERE
            h.sector = @Sector
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
            h.*,
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
    ELSE
    BEGIN
        RETURN;
    END
END;