param(
    [string]$OutputPath = (Join-Path $PSScriptRoot 'deploy-dev.sql')
)

$ErrorActionPreference = 'Stop'
$maximumLineLength = 2150

function ConvertTo-SqlCmdLines {
    param([Parameter(Mandatory)][string]$Sql)

    # Conserva el SQL; solo compacta separadores en espacios fuera de literales.
    $text = [System.Text.StringBuilder]::new()
    $inString = $false
    $inLineComment = $false
    $previousWasWhitespace = $false

    for ($index = 0; $index -lt $Sql.Length; $index++) {
        $character = $Sql[$index]
        $nextCharacter = if ($index + 1 -lt $Sql.Length) { $Sql[$index + 1] } else { [char]0 }

        if ($inLineComment) {
            [void]$text.Append($character)
            if ($character -eq "`n") {
                $inLineComment = $false
                $previousWasWhitespace = $true
            }
            continue
        }

        if (-not $inString -and $character -eq '-' -and $nextCharacter -eq '-') {
            if ($text.Length -gt 0 -and -not $previousWasWhitespace) { [void]$text.Append(' ') }
            [void]$text.Append('--')
            $index++
            $inLineComment = $true
            $previousWasWhitespace = $false
            continue
        }

        if ($character -eq "'") {
            [void]$text.Append($character)
            if ($inString -and $nextCharacter -eq "'") {
                [void]$text.Append($nextCharacter)
                $index++
            }
            else {
                $inString = -not $inString
            }
            $previousWasWhitespace = $false
            continue
        }

        if (-not $inString -and [char]::IsWhiteSpace($character)) {
            if ($text.Length -gt 0 -and -not $previousWasWhitespace) {
                [void]$text.Append(' ')
                $previousWasWhitespace = $true
            }
            continue
        }

        [void]$text.Append($character)
        $previousWasWhitespace = $false
    }

    $compactSql = $text.ToString().Trim()
    $lines = [System.Collections.Generic.List[string]]::new()
    while ($compactSql.Length -gt $maximumLineLength) {
        $cutAt = $compactSql.LastIndexOf(' ', $maximumLineLength)
        if ($cutAt -lt 1) { $cutAt = $maximumLineLength }
        $lines.Add($compactSql.Substring(0, $cutAt).TrimEnd())
        $compactSql = $compactSql.Substring($cutAt).TrimStart()
    }
    if ($compactSql.Length -gt 0) { $lines.Add($compactSql) }
    return $lines
}

function Add-SqlBatch {
    param(
        [Parameter(Mandatory)][System.Text.StringBuilder]$Builder,
        [Parameter(Mandatory)][string]$Sql,
        [Parameter(Mandatory)][string]$Name
    )

    [void]$Builder.AppendLine("-- $Name")
    foreach ($line in ConvertTo-SqlCmdLines -Sql $Sql) {
        [void]$Builder.AppendLine($line)
    }
    [void]$Builder.AppendLine('GO')
    [void]$Builder.AppendLine()
}

$manualChanges = @(
    @{ Name = 'Agregar campo entra_object_id a revisores'; Sql = "ALTER TABLE REVISORES ADD entra_object_id UNIQUEIDENTIFIER NULL" },
    @{ Name = 'Modificar columna Result de SyncControl'; Sql = "ALTER TABLE SyncControl ALTER COLUMN Result nvarchar(256);" },
    @{ Name = 'Agregar columna a Clientes_Creatio'; Sql = "IF COL_LENGTH('dbo.Clientes_Creatio', 'Hdr_Activo') IS NULL ALTER TABLE dbo.Clientes_Creatio ADD Hdr_Activo bit NOT NULL CONSTRAINT DF_Clientes_Creatio_Hdr_Activo DEFAULT (1) WITH VALUES;" },
    @{ Name = 'Agregar columnas a revisores y socios'; Sql = "IF COL_LENGTH('dbo.SOCIOS', 'Hdr_Activo') IS NULL ALTER TABLE dbo.SOCIOS ADD Hdr_Activo bit NOT NULL CONSTRAINT DF_SOCIOS_Hdr_Activo DEFAULT (1) WITH VALUES; IF COL_LENGTH('dbo.REVISORES', 'Hdr_Activo') IS NULL ALTER TABLE dbo.REVISORES ADD Hdr_Activo bit NOT NULL CONSTRAINT DF_REVISORES_Hdr_Activo DEFAULT (1) WITH VALUES;" },
    @{ Name = 'Agregar entra_object_id a socios y revisores'; Sql = "IF COL_LENGTH('dbo.SOCIOS', 'entra_object_id') IS NULL ALTER TABLE dbo.SOCIOS ADD entra_object_id NVARCHAR(100) NULL; IF COL_LENGTH('dbo.REVISORES', 'entra_object_id') IS NULL ALTER TABLE dbo.REVISORES ADD entra_object_id NVARCHAR(100) NULL;" },
    @{ Name = 'Agregar columna a sectores'; Sql = "IF COL_LENGTH('dbo.SECTORES', 'Hdr_Activo') IS NULL ALTER TABLE dbo.SECTORES ADD Hdr_Activo bit NOT NULL CONSTRAINT DF_SECTORES_Hdr_Activo DEFAULT (1) WITH VALUES;" },
    @{ Name = 'Crear tipo HDR_DirectoryUserIdentity'; Sql = "IF TYPE_ID(N'dbo.HDR_DirectoryUserIdentity') IS NULL EXEC(N'CREATE TYPE dbo.HDR_DirectoryUserIdentity AS TABLE (EntraObjectId NVARCHAR(100) NULL, Mail NVARCHAR(150) NULL)');" },
    @{ Name = 'Crear tipo HDR_DirectoryReconciliationIdentity'; Sql = "IF TYPE_ID(N'dbo.HDR_DirectoryReconciliationIdentity') IS NULL EXEC(N'CREATE TYPE dbo.HDR_DirectoryReconciliationIdentity AS TABLE (EntraObjectId NVARCHAR(100) NULL, Mail NVARCHAR(150) NULL, AllowMailFallback BIT NOT NULL)');" },
    @{ Name = 'Nueva tabla de errores'; Sql = "IF OBJECT_ID(N'dbo.ErrorLog', N'U') IS NULL BEGIN CREATE TABLE dbo.ErrorLog ( Id bigint IDENTITY(1,1) NOT NULL, IncidentId varchar(12) NOT NULL, OccurredAt datetime2(3) NOT NULL, ErrorCode varchar(80) NOT NULL, UserName nvarchar(256) NULL, HojaId nvarchar(128) NULL, OperationId varchar(64) NULL, Endpoint nvarchar(512) NOT NULL, UserMessage nvarchar(500) NOT NULL, ExceptionMessage nvarchar(4000) NOT NULL, CONSTRAINT PK_ErrorLog PRIMARY KEY CLUSTERED (Id), CONSTRAINT UX_ErrorLog_IncidentId UNIQUE (IncidentId) ); CREATE INDEX IX_ErrorLog_OccurredAt ON dbo.ErrorLog (OccurredAt); CREATE INDEX IX_ErrorLog_HojaId ON dbo.ErrorLog (HojaId); CREATE INDEX IX_ErrorLog_OperationId ON dbo.ErrorLog (OperationId); CREATE INDEX IX_ErrorLog_ErrorCode ON dbo.ErrorLog (ErrorCode); END" },
    @{ Name = 'Agregado a la tabla de errores'; Sql = "IF COL_LENGTH(N'dbo.ErrorLog', N'Fingerprint') IS NULL ALTER TABLE dbo.ErrorLog ADD Fingerprint varchar(64) NULL; IF COL_LENGTH(N'dbo.ErrorLog', N'ResolvedAt') IS NULL ALTER TABLE dbo.ErrorLog ADD ResolvedAt datetime2(3) NULL; IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ErrorLog') AND name = N'UX_ErrorLog_Fingerprint_Open') CREATE UNIQUE INDEX UX_ErrorLog_Fingerprint_Open ON dbo.ErrorLog (Fingerprint) WHERE Fingerprint IS NOT NULL AND ResolvedAt IS NULL;" },
    @{ Name = 'HOJAS verificar preparo_fecha'; Sql = "SELECT preparo_fecha,numeracion FROM hojas WHERE TRY_CONVERT(date, preparo_fecha, 103) IS NULL;" },
    @{ Name = 'HOJAS agregar columna temporal'; Sql = "ALTER TABLE hojas ADD FechaDate date;" },
    @{ Name = 'HOJAS actualizar columna temporal'; Sql = "UPDATE hojas SET FechaDate = TRY_CONVERT(date, preparo_fecha, 103)" },
    @{ Name = 'HOJAS reemplazar preparo_fecha'; Sql = "ALTER TABLE hojas DROP COLUMN preparo_fecha; EXEC sp_rename 'hojas.FechaDate', 'preparo_fecha', 'COLUMN';" },
    @{ Name = 'SOCIOS agregar sector'; Sql = "ALTER TABLE dbo.SOCIOS ADD sector NVARCHAR(50) NULL;" },
    @{ Name = 'SOCIOS completar sector desde revisores'; Sql = "UPDATE socio SET sector = revisor.Area FROM dbo.SOCIOS AS socio OUTER APPLY ( SELECT TOP (1) r.area FROM dbo.REVISORES AS r WHERE NULLIF(r.area, '') IS NOT NULL AND ( (NULLIF(socio.entra_object_id, '') IS NOT NULL AND socio.entra_object_id = r.entra_object_id) OR (NULLIF(socio.entra_object_id, '') IS NULL AND socio.mail = r.mail) ) ORDER BY r.area ) AS revisor WHERE NULLIF(socio.sector, '') IS NULL AND NULLIF(revisor.area, '') IS NOT NULL;" }
)

$storedProcedures = @(
    'sp_get_hojas_index_paged.sql',
    'sp_get_hojas_by_nivel.sql',
    'sp_get_hojas_by_nivel_base.sql',
    'sp_get_next_hoja_numero.sql',
    'sp_get_revisores_by_nivel.sql',
    'sp_get_hojas_for_reporte.sql',
    'sp_sync_usuarios_v2.sql',
    'sp_sync_usuario_directorio.sql',
    'sp_inactivar_usuarios_directorio_ausentes.sql',
    'sp_inactivar_usuarios_directorio_sin_hdr.sql',
    'sp_get_socio_lider_by_area.sql'
)

$builder = [System.Text.StringBuilder]::new()
[void]$builder.AppendLine('-- Hoja de Ruta - deploy DEV. Ejecutar UN bloque a la vez, en el orden listado.')
[void]$builder.AppendLine('-- Cada bloque termina con GO. Los SP se obtienen sin cambios lógicos desde StoredProcedures.')
[void]$builder.AppendLine()

foreach ($change in $manualChanges) {
    Add-SqlBatch -Builder $builder -Sql $change.Sql -Name $change.Name
}

foreach ($storedProcedure in $storedProcedures) {
    $path = Join-Path $PSScriptRoot "StoredProcedures\$storedProcedure"
    if (-not (Test-Path -LiteralPath $path)) { throw "No se encontró el stored procedure requerido: $path" }
    $batches = (Get-Content -Raw -LiteralPath $path) -split '(?im)^\s*GO\s*(?:--.*)?\r?\n'
    foreach ($batch in $batches) {
        if (-not [string]::IsNullOrWhiteSpace($batch)) {
            Add-SqlBatch -Builder $builder -Sql $batch -Name "SP $storedProcedure"
        }
    }
}

[System.IO.File]::WriteAllText($OutputPath, $builder.ToString(), [System.Text.UTF8Encoding]::new($false))
Write-Output "Generado: $OutputPath"
