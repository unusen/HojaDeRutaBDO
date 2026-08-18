/*
    La ejecución de este script es controlada. No crea un índice único: cada fila
    de SOCIOS representa la responsabilidad de un socio sobre un sector.
*/
IF COL_LENGTH('dbo.SOCIOS', 'sector') IS NULL
BEGIN
    ALTER TABLE dbo.SOCIOS ADD sector NVARCHAR(50) NULL;
END;

UPDATE socio
SET sector = revisor.Area
FROM dbo.SOCIOS AS socio
OUTER APPLY
(
    SELECT TOP (1) r.area
    FROM dbo.REVISORES AS r
    WHERE NULLIF(r.area, '') IS NOT NULL
      AND
      (
          (NULLIF(socio.entra_object_id, '') IS NOT NULL AND socio.entra_object_id = r.entra_object_id)
          OR
          (NULLIF(socio.entra_object_id, '') IS NULL AND socio.mail = r.mail)
      )
    ORDER BY r.area
) AS revisor
WHERE NULLIF(socio.sector, '') IS NULL
  AND NULLIF(revisor.area, '') IS NOT NULL;

DECLARE @AsignacionesMultiArea TABLE
(
    Detalle NVARCHAR(350) NOT NULL,
    Sector NVARCHAR(50) NOT NULL
);

INSERT INTO @AsignacionesMultiArea (Detalle, Sector)
VALUES
    (N'ROZEN, CARLOS FERNANDO', N'GRC'),
    (N'ROZEN, CARLOS FERNANDO', N'BPCO'),
    (N'ROZEN, CARLOS FERNANDO', N'CHMA'),
    (N'GARABATO, FERNANDO', N'DEAL'),
    (N'GARABATO, FERNANDO', N'BANK');

INSERT INTO dbo.SOCIOS (socio, detalle, mail, sector, liderDeArea, entra_object_id, Hdr_Activo)
SELECT origen.socio,
       origen.detalle,
       origen.mail,
       destino.Sector,
       1,
       origen.entra_object_id,
       origen.Hdr_Activo
FROM @AsignacionesMultiArea AS destino
OUTER APPLY
(
    SELECT TOP (1) s.*
    FROM dbo.SOCIOS AS s
    WHERE s.detalle = destino.Detalle
    ORDER BY s.Hdr_Activo DESC, s.sector
) AS origen
WHERE origen.mail IS NOT NULL
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.SOCIOS AS existente
      WHERE existente.mail = origen.mail
        AND existente.sector = destino.Sector
  );

/* Revisar este resultado antes de habilitar el cambio en producción. */
SELECT socio.socio,
       socio.detalle,
       socio.mail,
       socio.entra_object_id,
       socio.sector,
       CASE WHEN socio.sector IS NULL OR LTRIM(RTRIM(socio.sector)) = '' THEN N'Sin sector asociado' END AS Observacion
FROM dbo.SOCIOS AS socio
WHERE socio.Hdr_Activo = 1
  AND (socio.sector IS NULL OR LTRIM(RTRIM(socio.sector)) = '')
ORDER BY socio.detalle, socio.mail;

SELECT destino.Detalle,
       destino.Sector,
       CASE WHEN EXISTS
       (
           SELECT 1
           FROM dbo.SOCIOS AS socio
           WHERE socio.detalle = destino.Detalle
             AND socio.sector = destino.Sector
             AND socio.Hdr_Activo = 1
       ) THEN N'Configurado' ELSE N'Pendiente de carga manual' END AS Estado
FROM @AsignacionesMultiArea AS destino
ORDER BY destino.Detalle, destino.Sector;
