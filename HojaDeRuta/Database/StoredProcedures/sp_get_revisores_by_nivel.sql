CREATE PROCEDURE [dbo].[sp_get_revisores_by_nivel]
    @NivelActual INT = NULL
AS
BEGIN
    SELECT *
    FROM REVISORES
    WHERE cargo > @NivelActual
    ORDER BY detalle;
END;