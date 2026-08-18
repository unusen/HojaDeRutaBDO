CREATE PROCEDURE [dbo].[sp_get_socio_lider_by_area]
    @Area NVARCHAR(50) = NULL
AS
BEGIN
    SELECT
        s.*
    FROM socios s
    WHERE
        s.sector = @Area
        AND s.liderDeArea = 1
        AND s.Hdr_Activo = 1;
END;
