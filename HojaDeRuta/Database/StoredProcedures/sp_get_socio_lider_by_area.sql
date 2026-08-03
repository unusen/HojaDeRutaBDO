CREATE PROCEDURE [dbo].[sp_get_socio_lider_by_area]
    @Area NVARCHAR(50) = NULL
AS
BEGIN
    SELECT
        s.*
    FROM socios s
    INNER JOIN revisores r
        ON r.mail = s.mail
    WHERE
        r.area = @Area
        AND s.liderDeArea = 1;
END;