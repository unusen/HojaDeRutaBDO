# Job externo de sincronizacion de contratos

## Objetivo
Este job ejecuta en el server la misma logica funcional de contratos que hoy hace la app:

1. Lee `CodigoCliente,RazonSocial,Contrato,FechaAlta` desde `dbo.CONSULTA_CONTRATOS`.
2. Elimina solo los contratos no manuales (`EsManual = 0`).
3. Inserta nuevamente todos los contratos en lotes.

La diferencia es que usa el contenedor oficial `mcr.microsoft.com/mssql-tools`, que ya fue validado manualmente contra la base remota.

## Script
Archivo:

- [`scripts/sync_contratos_job.sh`](C:/0-Proyectos/BGlobal/BDO/HojaDeRuta/HojaDeRuta/scripts/sync_contratos_job.sh)

## Configuracion
El script ya viene preparado para que toda la configuracion viva dentro del archivo.

Revisar y completar estos valores al inicio de [`scripts/sync_contratos_job.sh`](C:/0-Proyectos/BGlobal/BDO/HojaDeRuta/HojaDeRuta/scripts/sync_contratos_job.sh):

- `REMOTE_SQL_HOST`
- `REMOTE_SQL_DB`
- `REMOTE_SQL_USER`
- `REMOTE_SQL_PASSWORD`
- `LOCAL_SQL_CONTAINER`
- `LOCAL_SQL_DB`
- `LOCAL_SQL_USER`
- `LOCAL_SQL_PASSWORD`
- `SQLCMD_IMAGE`
- `TEMP_DIR`

Por default quedaron cargados host, base y usuarios esperados, y los passwords con placeholder para reemplazar.

## Ejecucion manual
1. Copiar el script al server.
2. Dar permisos:

```bash
chmod +x /ruta/al/script/sync_contratos_job.sh
```

3. Editar el script y completar los passwords.
4. Ejecutar:

```bash
/ruta/al/script/sync_contratos_job.sh
```

## Que valida el script
- que exista `docker`
- que el contenedor local `hoja_sql` este corriendo
- que la extraccion remota devuelva filas
- que el `DELETE de no manuales + INSERT` local termine sin error

## Archivos temporales
Cada corrida genera una carpeta tipo:

```bash
/tmp/hdr_sync_contratos/run_YYYYMMDD_HHMMSS
```

Adentro deja:

- `contratos.tsv`
- `sync.log`
- `sync_contratos.sql`

## Programacion nocturna con cron
Editar el crontab del usuario que tenga acceso a Docker:

```bash
crontab -e
```

Agregar algo como esto para correr todos los dias a las 02:10:

```cron
10 2 * * * /ruta/al/script/sync_contratos_job.sh >> /var/log/hdr_sync_contratos_cron.log 2>&1
```

## Recomendacion operativa
- Primero probar manualmente.
- Verificar que `CONTRATOS_COMPLETO` quede cargada.
- Recién despues dejarlo en `cron`.

## Verificacion posterior
Consultar cantidad de filas en local:

```sql
SELECT COUNT(*) FROM CONTRATOS_COMPLETO;
```

Y revisar las ultimas carpetas en:

```bash
ls -ltr /tmp/hdr_sync_contratos
```
