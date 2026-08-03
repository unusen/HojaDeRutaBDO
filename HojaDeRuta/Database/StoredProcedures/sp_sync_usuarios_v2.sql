CREATE PROCEDURE [dbo].[sp_sync_usuarios_v2]
    @username VARCHAR(100),
    @empleado VARCHAR(50),
    @email VARCHAR(150),
    @area VARCHAR(50),
    @nivel INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @resultado INT = 0;
    DECLARE @mailLegacy VARCHAR(150);

    SET @mailLegacy = UPPER(
        LEFT(
            ISNULL(@email, ''),
            CHARINDEX('@', ISNULL(@email, '') + '@') - 1
        )
    );

    IF @nivel IN (9, 10)
    BEGIN
        IF EXISTS
        (
            SELECT 1
            FROM socios
            WHERE mail = @mailLegacy
        )
        BEGIN
            IF EXISTS
            (
                SELECT 1
                FROM socios
                WHERE mail = @mailLegacy
                  AND
                  (
                      ISNULL(detalle, '') <> ISNULL(@username, '')
                      OR ISNULL(socio, '') <> ISNULL(LEFT(@empleado, 3), '')
                      OR ISNULL(liderDeArea, 0) <> CASE
                                                     WHEN @nivel = 10 THEN 1
                                                     ELSE 0
                                                 END
                  )
            )
            BEGIN
                UPDATE socios
                SET
                    detalle = @username,
                    socio = LEFT(@empleado, 3),
                    liderDeArea = CASE
                                     WHEN @nivel = 10 THEN 1
                                     ELSE 0
                                 END
                WHERE mail = @mailLegacy;

                SET @resultado = 2;
            END
        END
        ELSE
        BEGIN
            INSERT INTO socios
            (
                socio,
                detalle,
                mail,
                liderDeArea
            )
            VALUES
            (
                LEFT(@empleado, 3),
                @username,
                @mailLegacy,
                CASE
                    WHEN @nivel = 10 THEN 1
                    ELSE 0
                END
            );

            SET @resultado = 1;
        END

        IF EXISTS
        (
            SELECT 1
            FROM revisores
            WHERE empleado = @empleado
        )
        BEGIN
            IF EXISTS
            (
                SELECT 1
                FROM revisores
                WHERE empleado = @empleado
                  AND
                  (
                      ISNULL(detalle, '') <> ISNULL(@username, '')
                      OR ISNULL(mail, '') <> ISNULL(@mailLegacy, '')
                      OR ISNULL(cargo, -1) <> ISNULL(@nivel, -1)
                      OR ISNULL(area, '') <> ISNULL(@area, '')
                  )
            )
            BEGIN
                UPDATE revisores
                SET
                    detalle = @username,
                    mail = @mailLegacy,
                    cargo = @nivel,
                    area = @area
                WHERE empleado = @empleado;

                SET @resultado = 2;
            END
        END
        ELSE
        BEGIN
            INSERT INTO revisores
            (
                empleado,
                detalle,
                mail,
                cargo,
                area,
                subarea
            )
            VALUES
            (
                @empleado,
                @username,
                @mailLegacy,
                @nivel,
                @area,
                NULL
            );

            SET @resultado = 1;
        END

        RETURN @resultado;
    END

    DELETE FROM socios
    WHERE mail = @mailLegacy;

    IF EXISTS
    (
        SELECT 1
        FROM revisores
        WHERE empleado = @empleado
    )
    BEGIN
        IF EXISTS
        (
            SELECT 1
            FROM revisores
            WHERE empleado = @empleado
              AND
              (
                  ISNULL(detalle, '') <> ISNULL(@username, '')
                  OR ISNULL(mail, '') <> ISNULL(@mailLegacy, '')
                  OR ISNULL(cargo, -1) <> ISNULL(@nivel, -1)
                  OR ISNULL(area, '') <> ISNULL(@area, '')
              )
        )
        BEGIN
            UPDATE revisores
            SET
                detalle = @username,
                mail = @mailLegacy,
                cargo = @nivel,
                area = @area
            WHERE empleado = @empleado;

            SET @resultado = 2;
        END
    END
    ELSE
    BEGIN
        INSERT INTO revisores
        (
            empleado,
            detalle,
            mail,
            cargo,
            area,
            subarea
        )
        VALUES
        (
            @empleado,
            @username,
            @mailLegacy,
            @nivel,
            @area,
            NULL
        );

        SET @resultado = 1;
    END

    RETURN @resultado;
END