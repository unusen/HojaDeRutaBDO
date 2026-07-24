#!/usr/bin/env bash
set -euo pipefail

# Sincroniza CONTRATOS_COMPLETO usando el contenedor oficial mssql-tools,
# evitando el problema TLS observado desde la app web.
#
# Requisitos:
# - Docker instalado en el server
# - Contenedor SQL local accesible (por default: hoja_sql)
# - Imagen mcr.microsoft.com/mssql-tools disponible o descargable
#
# Configuracion fija del job.
# Completar los passwords antes de usarlo en el server.
REMOTE_SQL_HOST="tcp:172.16.47.20,1433"
REMOTE_SQL_DB="Qview"
REMOTE_SQL_USER="HDR"
REMOTE_SQL_PASSWORD="REEMPLAZAR_PASSWORD_REMOTO"

LOCAL_SQL_CONTAINER="hoja_sql"
LOCAL_SQL_DB="hojas_ruta_1"
LOCAL_SQL_USER="sa"
LOCAL_SQL_PASSWORD="REEMPLAZAR_PASSWORD_LOCAL"

SQLCMD_IMAGE="mcr.microsoft.com/mssql-tools"
TEMP_DIR="/tmp/hdr_sync_contratos"

WORK_DIR="${TEMP_DIR}/run_$(date +%Y%m%d_%H%M%S)"
DATA_FILE="${WORK_DIR}/contratos.tsv"
LOG_FILE="${WORK_DIR}/sync.log"
SQL_FILE="${WORK_DIR}/sync_contratos.sql"

mkdir -p "${WORK_DIR}"

log() {
  printf '[%s] %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$*" | tee -a "${LOG_FILE}"
}

fail() {
  log "ERROR: $*"
  exit 1
}

require_value() {
  local name="$1"
  local value="$2"
  if [[ -z "${value}" ]]; then
    fail "La variable ${name} es obligatoria."
  fi
}

require_value "REMOTE_SQL_PASSWORD" "${REMOTE_SQL_PASSWORD}"
require_value "LOCAL_SQL_PASSWORD" "${LOCAL_SQL_PASSWORD}"

if [[ "${REMOTE_SQL_PASSWORD}" == REEMPLAZAR_* ]]; then
  fail "Debes completar REMOTE_SQL_PASSWORD dentro del script."
fi

if [[ "${LOCAL_SQL_PASSWORD}" == REEMPLAZAR_* ]]; then
  fail "Debes completar LOCAL_SQL_PASSWORD dentro del script."
fi

if ! command -v docker >/dev/null 2>&1; then
  fail "docker no esta instalado o no esta en PATH."
fi

if ! docker ps --format '{{.Names}}' | grep -qx "${LOCAL_SQL_CONTAINER}"; then
  fail "No se encontro el contenedor local ${LOCAL_SQL_CONTAINER} en ejecucion."
fi

log "Iniciando extraccion remota de contratos."
log "RemoteHost=${REMOTE_SQL_HOST} RemoteDb=${REMOTE_SQL_DB} LocalContainer=${LOCAL_SQL_CONTAINER} LocalDb=${LOCAL_SQL_DB}"

docker run --rm \
  -e SQLCMDPASSWORD="${REMOTE_SQL_PASSWORD}" \
  "${SQLCMD_IMAGE}" \
  /opt/mssql-tools/bin/sqlcmd \
  -S "${REMOTE_SQL_HOST}" \
  -d "${REMOTE_SQL_DB}" \
  -U "${REMOTE_SQL_USER}" \
  -W \
  -h -1 \
  -s $'\t' \
  -Q "SET NOCOUNT ON; SELECT CodigoCliente,RazonSocial,Contrato,FechaAlta FROM dbo.CONSULTA_CONTRATOS WHERE CodigoCliente IS NOT NULL AND LTRIM(RTRIM(CodigoCliente)) <> '' AND Contrato IS NOT NULL AND LTRIM(RTRIM(Contrato)) <> ''" \
  > "${DATA_FILE}"

if [[ ! -s "${DATA_FILE}" ]]; then
  fail "La extraccion remota no devolvio datos."
fi

TOTAL_ROWS=$(awk 'NF > 0 { count++ } END { print count+0 }' "${DATA_FILE}")
log "Extraccion remota completada. Filas=${TOTAL_ROWS}"

python3 - "${DATA_FILE}" "${SQL_FILE}" <<'PY'
import sys

data_file = sys.argv[1]
sql_file = sys.argv[2]

def sql_escape(value: str) -> str:
    return value.replace("'", "''")

with open(data_file, "r", encoding="utf-8", errors="ignore") as src, open(sql_file, "w", encoding="utf-8") as dst:
    dst.write("SET NOCOUNT ON;\n")
    dst.write("BEGIN TRY\n")
    dst.write("BEGIN TRANSACTION;\n")
    dst.write("DELETE FROM CONTRATOS_COMPLETO WHERE ISNULL(EsManual, 0) = 0;\n")

    batch = []
    batch_size = 500

    def flush_batch(rows):
        if not rows:
            return
        dst.write("INSERT INTO CONTRATOS_COMPLETO (CodigoPlataforma, RazonSocial, Contrato, FechaAlta) VALUES\n")
        dst.write(",\n".join(rows))
        dst.write(";\n")

    for raw_line in src:
        line = raw_line.rstrip("\r\n")
        if not line.strip():
            continue

        parts = line.split("\t")
        if len(parts) < 4:
            continue

        codigo, razon_social, contrato, fecha_alta = [part.strip() for part in parts[:4]]
        if not codigo or not contrato:
            continue

        fecha_sql = "NULL"
        if fecha_alta:
            escaped_fecha = sql_escape(fecha_alta)
            fecha_sql = f"TRY_CONVERT(date, '{escaped_fecha}', 103)"

        razon_sql = "NULL" if not razon_social else f"N'{sql_escape(razon_social)}'"
        row = f"(N'{sql_escape(codigo)}', {razon_sql}, N'{sql_escape(contrato)}', {fecha_sql})"
        batch.append(row)

        if len(batch) >= batch_size:
            flush_batch(batch)
            batch = []

    flush_batch(batch)
    dst.write("COMMIT TRANSACTION;\n")
    dst.write("END TRY\n")
    dst.write("BEGIN CATCH\n")
    dst.write("IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;\n")
    dst.write("THROW;\n")
    dst.write("END CATCH;\n")
PY

log "Script SQL generado. Ejecutando carga local."

docker exec -i \
  -e SQLCMDPASSWORD="${LOCAL_SQL_PASSWORD}" \
  "${LOCAL_SQL_CONTAINER}" \
  /opt/mssql-tools/bin/sqlcmd \
  -S localhost \
  -d "${LOCAL_SQL_DB}" \
  -U "${LOCAL_SQL_USER}" \
  -b \
  -i /dev/stdin < "${SQL_FILE}"

log "Carga local finalizada correctamente."
log "Archivos temporales disponibles en ${WORK_DIR}"
