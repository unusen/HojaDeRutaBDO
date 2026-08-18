using System.Diagnostics;
using System.Data;
using System.Globalization;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HojaDeRuta.DBContext;
using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;
using HojaDeRuta.Services.Repository;
using HojaDeRuta.Services.LoginService;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HojaDeRuta.Services
{
    public class SyncService : BackgroundService
    {
        private const string ClientesSyncEntityName = "Clientes_Creatio";
        private const string UsuariosDirectorioSyncEntityName = "Usuarios_Entra";
        private const int SyncControlResultMaxLength = 255;
        private static readonly string[] SqlCmdCandidatePaths =
        {
            "/opt/mssql-tools/bin/sqlcmd",
            "/opt/mssql-tools18/bin/sqlcmd"
        };
        private static readonly TimeSpan SchedulerRetryDelay = TimeSpan.FromMinutes(5);
        private readonly ClientSyncReconciler _clientSyncReconciler = new();
        private DateOnly? _lastDailySyncAttemptDate;
        private readonly IServiceProvider _serviceProvider;
        private readonly SyncSettings _syncSettings;
        private readonly ILogger<SyncService> _logger;

        public SyncService(
            IServiceProvider serviceProvider,
            IOptions<SyncSettings> options,
            ILogger<SyncService> logger)
        {
            _serviceProvider = serviceProvider;
            _syncSettings = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;
                    _logger.LogInformation("Ejecucion del servicio de sincronizacion a las {Time}", now);

                    var dailyScheduledAt = GetDailyScheduledTime(now);
                    var weeklyScheduledAt = GetWeeklyScheduledTime(now);
                    var lastClientesSync = await GetLastSuccessfulOrRecordedSyncOrNull(ClientesSyncEntityName);
                    var lastContratosSync = await GetLastSuccessfulOrRecordedSyncOrNull("contratos_completo");
                    var lastUsuariosDirectorioSync = await GetLastSuccessfulOrRecordedSyncOrNull(UsuariosDirectorioSyncEntityName);
                    var lastWeeklyNotification = await GetLastSuccessfulOrRecordedSyncOrNull("Email_Pendientes");

                    var dailyAttemptedToday = _lastDailySyncAttemptDate == DateOnly.FromDateTime(dailyScheduledAt);
                    var shouldRunDaily =
                        now >= dailyScheduledAt &&
                        !dailyAttemptedToday &&
                        (!lastClientesSync.HasValue || lastClientesSync.Value < dailyScheduledAt
                         || !lastContratosSync.HasValue || lastContratosSync.Value < dailyScheduledAt
                         || !lastUsuariosDirectorioSync.HasValue || lastUsuariosDirectorioSync.Value < dailyScheduledAt);

                    var shouldRunWeekly =
                        now >= weeklyScheduledAt &&
                        (!lastWeeklyNotification.HasValue || lastWeeklyNotification.Value < weeklyScheduledAt);

                    _logger.LogInformation(
                        "Estado scheduler. DailyScheduledAt={DailyScheduledAt}; WeeklyScheduledAt={WeeklyScheduledAt}; LastClientesSync={LastClientesSync}; LastContratosSync={LastContratosSync}; LastUsuariosDirectorioSync={LastUsuariosDirectorioSync}; LastWeeklyNotification={LastWeeklyNotification}; ShouldRunDaily={ShouldRunDaily}; ShouldRunWeekly={ShouldRunWeekly}",
                        dailyScheduledAt,
                        weeklyScheduledAt,
                        lastClientesSync,
                        lastContratosSync,
                        lastUsuariosDirectorioSync,
                        lastWeeklyNotification,
                        shouldRunDaily,
                        shouldRunWeekly);

                    if (shouldRunDaily)
                    {
                        try
                        {
                            await ExecuteDailySyncAsync(stoppingToken, dailyScheduledAt);
                        }
                        finally
                        {
                            _lastDailySyncAttemptDate = DateOnly.FromDateTime(dailyScheduledAt);
                        }

                        continue;
                    }

                    if (shouldRunWeekly)
                    {
                        await ExecuteWeeklyNotificationAsync(stoppingToken, weeklyScheduledAt);
                        continue;
                    }

                    var nextRunDaily = now < dailyScheduledAt ? dailyScheduledAt : GetDailyScheduledTime(now.AddDays(1));
                    var nextRunWeekly = now < weeklyScheduledAt ? weeklyScheduledAt : GetWeeklyScheduledTime(now.AddDays(1));
                    var nextRun = nextRunDaily < nextRunWeekly ? nextRunDaily : nextRunWeekly;
                    var timeUntilNextRun = nextRun - now;

                    _logger.LogInformation("Proxima ejecucion diaria: {DailyRun}. Proxima ejecucion semanal: {WeeklyRun}", nextRunDaily, nextRunWeekly);

                    if (timeUntilNextRun < TimeSpan.Zero)
                    {
                        timeUntilNextRun = TimeSpan.Zero;
                    }

                    await Task.Delay(timeUntilNextRun, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "El scheduler de sincronizacion no pudo consultar su estado actual. Se reintentara en {RetryDelayMinutes} minutos sin detener la aplicacion.",
                        SchedulerRetryDelay.TotalMinutes);

                    await Task.Delay(SchedulerRetryDelay, stoppingToken);
                }
            }
        }

        public async Task SyncContacts(CancellationToken token)
        {
            var syncControl = new SyncControl
            {
                EntityName = ClientesSyncEntityName
            };
            var stopwatch = Stopwatch.StartNew();

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HojasDbContext>();
            var creatioService = scope.ServiceProvider.GetRequiredService<CreatioService>();
            var catalogCacheService = scope.ServiceProvider.GetRequiredService<ICatalogCacheService>();

            try
            {
                _logger.LogInformation("Inicio de reconciliacion completa de clientes sincronizables con Creatio.");

                var clientesCreatio = creatioService.GetClientesSincronizables();
                var clientesLocales = await db.Clientes_Creatio
                    .AsNoTracking()
                    .OrderBy(cliente => cliente.Id)
                    .ToListAsync(token);

                _logger.LogInformation(
                    "Clientes sincronizables obtenidos desde Creatio. RemoteCount={RemoteCount} LocalCount={LocalCount}",
                    clientesCreatio.Count,
                    clientesLocales.Count);

                if (clientesCreatio.Count == 0)
                {
                    stopwatch.Stop();
                    syncControl.LastSyncDate = DateTime.UtcNow;
                    syncControl.Result = BuildClientSyncSummary(
                        remoteCount: 0,
                        localCount: clientesLocales.Count,
                        insertedCount: 0,
                        reactivatedCount: 0,
                        candidateInactivationCount: 0,
                        confirmedInactivationCount: 0,
                        inactivatedCount: 0,
                        skippedCount: 0,
                        reason: "Se bloqueo la reconciliacion porque Creatio devolvio 0 clientes sincronizables.");

                    _logger.LogWarning(syncControl.Result);
                    await CreateSyncControl(syncControl);
                    return;
                }

                var plan = _clientSyncReconciler.BuildPlan(clientesCreatio, clientesLocales);
                var shouldBlockDeletions = _clientSyncReconciler.ShouldBlockDeletions(plan.RemoteActiveCount, plan.LocalCount);
                var insertedCount = 0;
                var reactivatedCount = 0;
                var confirmedInactivationCount = 0;
                var inactivatedCount = 0;
                var skippedCount = 0;

                if (plan.ClientsToInsert.Any())
                {
                    _logger.LogInformation("Clientes nuevos detectados para insertar. Count={Count}", plan.ClientsToInsert.Count);
                    await db.Clientes_Creatio.AddRangeAsync(plan.ClientsToInsert, token);
                    insertedCount = await db.SaveChangesAsync(token);
                    db.ChangeTracker.Clear();
                }

                if (plan.ClientsToReactivate.Any())
                {
                    var idsToReactivate = plan.ClientsToReactivate.Select(cliente => cliente.Id).ToList();
                    var trackedClientsToReactivate = await db.Clientes_Creatio
                        .Where(cliente => idsToReactivate.Contains(cliente.Id))
                        .ToListAsync(token);

                    foreach (var cliente in trackedClientsToReactivate)
                    {
                        cliente.Hdr_Activo = true;
                    }

                    reactivatedCount = await db.SaveChangesAsync(token);
                    db.ChangeTracker.Clear();
                    _logger.LogInformation("Clientes reactivados por presencia en Creatio. Count={Count}", reactivatedCount);
                }

                var manualClientsSkipped = plan.ClientsCandidateToDelete.Count(cliente => cliente.EsManual);
                var candidatesToDelete = plan.ClientsCandidateToDelete
                    .Where(cliente => !cliente.EsManual)
                    .ToList();
                var clientsToInactivate = new List<Clientes>();
                skippedCount += manualClientsSkipped;

                if (manualClientsSkipped > 0)
                {
                    _logger.LogInformation(
                        "Se excluyeron {ManualCount} clientes manuales de las bajas de sincronizacion.",
                        manualClientsSkipped);
                }

                if (candidatesToDelete.Any())
                {
                    if (shouldBlockDeletions)
                    {
                        skippedCount += candidatesToDelete.Count;
                        _logger.LogWarning(
                            "Se bloquearon las bajas de clientes por respuesta remota sospechosa. RemoteCount={RemoteCount} LocalCount={LocalCount} Candidates={CandidateCount}",
                            plan.RemoteActiveCount,
                            plan.LocalCount,
                            candidatesToDelete.Count);
                    }
                    else
                    {
                        foreach (var candidate in candidatesToDelete)
                        {
                            if (await HasHistoricalReferencesAsync(db, candidate.Id, token))
                            {
                                clientsToInactivate.Add(candidate);
                                _logger.LogWarning(
                                    "Se marcara inactivo el cliente {ClientId}/{CodigoPlataforma} porque tiene hojas historicas asociadas.",
                                    candidate.Id,
                                    candidate.CodigoPlataforma);
                                continue;
                            }

                            try
                            {
                                var clientesSincronizables = creatioService.GetClientesSincronizables(candidate.CodigoPlataforma);
                                if (clientesSincronizables.Any())
                                {
                                    skippedCount++;
                                    _logger.LogInformation(
                                        "Doble check de baja descartado para cliente {ClientId}/{CodigoPlataforma}: continua sincronizable en Creatio.",
                                        candidate.Id,
                                        candidate.CodigoPlataforma);
                                    continue;
                                }

                                clientsToInactivate.Add(candidate);
                            }
                            catch (Exception ex)
                            {
                                skippedCount++;
                                _logger.LogWarning(
                                    ex,
                                    "No se pudo validar la baja del cliente {ClientId}/{CodigoPlataforma} en Creatio. Se omite la eliminacion.",
                                    candidate.Id,
                                    candidate.CodigoPlataforma);
                            }
                        }
                    }
                }

                confirmedInactivationCount = clientsToInactivate.Count;
                if (clientsToInactivate.Any())
                {
                    var idsToInactivate = clientsToInactivate.Select(cliente => cliente.Id).Distinct().ToList();
                    var trackedClientsToInactivate = await db.Clientes_Creatio
                        .Where(cliente => idsToInactivate.Contains(cliente.Id))
                        .ToListAsync(token);

                    foreach (var cliente in trackedClientsToInactivate)
                    {
                        cliente.Hdr_Activo = false;
                    }

                    inactivatedCount = await db.SaveChangesAsync(token);
                    db.ChangeTracker.Clear();
                    _logger.LogInformation("Clientes marcados inactivos por ausencia en Creatio. Count={Count}", inactivatedCount);
                }

                if (insertedCount > 0 || reactivatedCount > 0 || inactivatedCount > 0)
                {
                    await catalogCacheService.InvalidateClientesAsync(token);
                    _logger.LogInformation("Cache distribuida de clientes invalidada tras la reconciliacion.");
                }

                stopwatch.Stop();
                syncControl.LastSyncDate = DateTime.UtcNow;
                syncControl.Result = BuildClientSyncSummary(
                    remoteCount: plan.RemoteActiveCount,
                    localCount: plan.LocalCount,
                    insertedCount: insertedCount,
                    reactivatedCount: reactivatedCount,
                    candidateInactivationCount: candidatesToDelete.Count,
                    confirmedInactivationCount: confirmedInactivationCount,
                    inactivatedCount: inactivatedCount,
                    skippedCount: skippedCount,
                    reason: $"duracionMs={stopwatch.ElapsedMilliseconds}");

                _logger.LogInformation(syncControl.Result);
                await CreateSyncControl(syncControl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la sincronizacion de clientes con Creatio.");
                await ReportJobErrorAsync(ex, "HDR-SYNC-CREATIO-001", "La sincronización de clientes no pudo completarse.", "SyncContacts", token);
                syncControl.LastSyncDate = DateTime.UtcNow;
                syncControl.Result = "Error en la sincronizacion de clientes. Verifique el log de eventos.";
                await CreateSyncControl(syncControl);
            }
        }

        public async Task NotificacionHojasPendientes(CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Comienzo de NotificacionHojasPendientes");

            var syncControl = new SyncControl
            {
                EntityName = "Email_Pendientes",
                LastSyncDate = DateTime.UtcNow
            };

            using var scope = _serviceProvider.CreateScope();
            var notificationDeliveryService = scope.ServiceProvider.GetRequiredService<INotificationDeliveryService>();
            var hojasService = scope.ServiceProvider.GetRequiredService<HojaDeRutaService>();

            try
            {
                var pendientes = await hojasService.GetHojasPendientes();
                var totalHojasPendientes = pendientes.Sum(pendiente => pendiente.CantidadRegistros);
                var enviadosOk = 0;
                var enviadosConError = 0;

                _logger.LogInformation(
                    "Hojas pendientes para notificar. Destinatarios={Recipients} HojasPendientes={PendingSheets}",
                    pendientes.Count,
                    totalHojasPendientes);

                if (pendientes.Any())
                {
                    foreach (var pendiente in pendientes)
                    {
                        try
                        {
                            await notificationDeliveryService.SendWeeklyPendingAsync(pendiente);
                            enviadosOk++;

                            _logger.LogInformation(
                                "Notificacion semanal enviada. Revisor={Reviewer} CantidadHojas={PendingCount}",
                                pendiente.Revisor,
                                pendiente.CantidadRegistros);
                        }
                        catch (Exception ex)
                        {
                            enviadosConError++;
                            _logger.LogWarning(
                                ex,
                                "Fallo el envio semanal para el revisor {Reviewer}. CantidadHojas={PendingCount}",
                                pendiente.Revisor,
                                pendiente.CantidadRegistros);
                        }
                    }

                    stopwatch.Stop();
                    syncControl.Result = $"Sync semanal: destinatarios={pendientes.Count}; hojasPendientes={totalHojasPendientes}; enviadosOk={enviadosOk}; enviadosConError={enviadosConError}; duracionMs={stopwatch.ElapsedMilliseconds}.";
                }
                else
                {
                    stopwatch.Stop();
                    syncControl.Result = $"No se encontraron hojas pendientes para el envio semanal. duracionMs={stopwatch.ElapsedMilliseconds}.";
                }

                _logger.LogInformation(syncControl.Result);
                await CreateSyncControl(syncControl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el envio masivo de notificaciones semanales.");
                await ReportJobErrorAsync(ex, "HDR-NOTIFY-WEEKLY-001", "El envío semanal de notificaciones no pudo completarse.", "NotificacionHojasPendientes", token);
                syncControl.Result = "Error en el envio de notificaciones semanales.";
                await CreateSyncControl(syncControl);
            }
        }

        public async Task SyncContratos(CancellationToken token)
        {
            _logger.LogInformation("Comienzo de SyncContratos");
            var stopwatch = Stopwatch.StartNew();

            var syncControl = new SyncControl
            {
                EntityName = "contratos_completo",
                LastSyncDate = DateTime.UtcNow
            };

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HojasDbContext>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var catalogCacheService = scope.ServiceProvider.GetRequiredService<ICatalogCacheService>();
            var remoteConnectionString = configuration.GetConnectionString("vistaContratos");

            if (string.IsNullOrWhiteSpace(remoteConnectionString))
            {
                throw new InvalidOperationException("Falta configurar ConnectionStrings:vistaContratos.");
            }

            _logger.LogInformation(
                "Diagnostico vistaContratos. {ConnectionSummary}",
                BuildSqlConnectionDiagnostic(remoteConnectionString));

            try
            {
                var remoteResult = await ObtenerContratosDesdeVista(remoteConnectionString, token);
                var vistaContratos = remoteResult.Contratos;
                _logger.LogInformation("Se obtuvieron {Count} contratos desde la vista remota.", vistaContratos.Count);

                if (vistaContratos.Count == 0)
                {
                    stopwatch.Stop();
                    syncControl.Result = $"La vista remota no devolvio contratos. No se realizaron cambios. duracionMs={stopwatch.ElapsedMilliseconds}.";
                    _logger.LogWarning(syncControl.Result);
                    await CreateSyncControl(syncControl);
                    return;
                }

                await using var transaction = await db.Database.BeginTransactionAsync(token);

                try
                {
                    var deletedRows = await db.Database.ExecuteSqlRawAsync(
                        "DELETE FROM CONTRATOS_COMPLETO WHERE ISNULL(EsManual, 0) = 0",
                        token);
                    _logger.LogInformation(
                        "Se eliminaron {DeletedRows} contratos no manuales de CONTRATOS_COMPLETO antes de la recarga.",
                        deletedRows);

                    const int batchSize = 500;
                    int totalInsertados = 0;

                    for (int i = 0; i < vistaContratos.Count; i += batchSize)
                    {
                        var batch = vistaContratos.Skip(i).Take(batchSize).ToList();
                        await db.CONTRATOS_COMPLETO.AddRangeAsync(batch, token);
                        await db.SaveChangesAsync(token);
                        db.ChangeTracker.Clear();
                        totalInsertados += batch.Count;
                        _logger.LogInformation("Batch insertado: {Inserted}/{Total} contratos.", totalInsertados, vistaContratos.Count);
                    }

                    await transaction.CommitAsync(token);
                    await catalogCacheService.InvalidateContratosAsync(token);
                    stopwatch.Stop();

                    syncControl.Result =
                        $"Sync contratos: leidos={remoteResult.TotalLeidos}; insertados={totalInsertados}; " +
                        $"fechaAltaValida={remoteResult.FechasValidas}; fechaAltaInvalidaONula={remoteResult.FechasInvalidasONulas}; " +
                        $"duracionMs={stopwatch.ElapsedMilliseconds}.";
                    _logger.LogInformation(syncControl.Result);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(token);
                    _logger.LogError(ex, "Error durante delete/insert de contratos. Se hizo rollback.");
                    throw;
                }

                await CreateSyncControl(syncControl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falla general en SyncContratos.");
                await ReportJobErrorAsync(ex, "HDR-SYNC-CONTRATOS-001", "La sincronización de contratos no pudo completarse.", "SyncContratos", token);
                syncControl.Result = TruncateSyncResult(
                    $"Error al sincronizar contratos con la vista remota. {BuildExceptionSummary(ex)}");
                await CreateSyncControl(syncControl);
            }
        }

        private async Task<ContratosRemoteLoadResult> ObtenerContratosDesdeVista(string connectionString, CancellationToken token)
        {
            var resultado = new List<Contratos>();
            var fechasValidas = 0;
            var fechasInvalidasONulas = 0;

            const string query = @"
                SELECT CodigoCliente, RazonSocial, Contrato, FechaAlta
                FROM dbo.CONSULTA_CONTRATOS
                WHERE CodigoCliente IS NOT NULL
                  AND LTRIM(RTRIM(CodigoCliente)) <> ''
                  AND Contrato IS NOT NULL
                  AND LTRIM(RTRIM(Contrato)) <> ''";

            try
            {
                _logger.LogInformation("Intentando abrir conexion SQL remota para contratos.");
                using var remoteConn = new SqlConnection(connectionString);
                await remoteConn.OpenAsync(token);
                _logger.LogInformation(
                    "Conexion SQL remota abierta correctamente. DataSource={DataSource}; Database={Database}; ServerVersion={ServerVersion}",
                    remoteConn.DataSource,
                    remoteConn.Database,
                    remoteConn.ServerVersion);

                _logger.LogInformation("Ejecutando consulta remota de contratos.");
                using var cmd = new SqlCommand(query, remoteConn) { CommandTimeout = 120 };
                using var reader = await cmd.ExecuteReaderAsync(token);
                _logger.LogInformation("Consulta remota de contratos ejecutada correctamente. Procesando filas.");

                int colCodigo = reader.GetOrdinal("CodigoCliente");
                int colRazonSocial = reader.GetOrdinal("RazonSocial");
                int colContrato = reader.GetOrdinal("Contrato");
                int colFechaAlta = reader.GetOrdinal("FechaAlta");

                while (await reader.ReadAsync(token))
                {
                    AddContratoRow(
                        resultado,
                        ReadTrimmedString(reader, colCodigo),
                        ReadTrimmedString(reader, colRazonSocial),
                        ReadTrimmedString(reader, colContrato),
                        ReadTrimmedString(reader, colFechaAlta),
                        ref fechasValidas,
                        ref fechasInvalidasONulas);
                }
            }
            catch (Exception ex) when (IsTlsHandshakeFailure(ex))
            {
                _logger.LogWarning(
                    ex,
                    "Fallo la lectura remota de contratos con SqlClient durante el pre-login TLS. Se intentara fallback con sqlcmd.");

                foreach (var contrato in await ObtenerContratosDesdeVistaConSqlCmd(connectionString, $"SET NOCOUNT ON; {query}", token))
                {
                    AddContratoRow(
                        resultado,
                        contrato.CodigoPlataforma,
                        contrato.RazonSocial,
                        contrato.Contrato,
                        contrato.FechaAltaRaw,
                        ref fechasValidas,
                        ref fechasInvalidasONulas);
                }
            }

            _logger.LogInformation("Lectura de vista remota finalizada: {Count} registros.", resultado.Count);
            return new ContratosRemoteLoadResult(resultado, resultado.Count, fechasValidas, fechasInvalidasONulas);
        }

        public async Task<DateTime?> GetLastSync(string entityNameValue)
        {
            try
            {
                _logger.LogInformation("Comienzo de GetLastSync para la entidad {EntityName}", entityNameValue);

                using var scope = _serviceProvider.CreateScope();
                var syncControlRepository = scope.ServiceProvider.GetRequiredService<IGenericRepository<SyncControl>>();

                Expression<Func<SyncControl, bool>> entityName = s => s.EntityName == entityNameValue;
                Expression<Func<SyncControl, object>> lastSync = s => s.LastSyncDate;

                var sync = await syncControlRepository.GetFirstOrLastAsync(entityName, lastSync, false);
                return sync?.LastSyncDate ?? DateTime.MinValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al recuperar la ultima sincronizacion para la entidad {EntityName}", entityNameValue);
                throw new Exception("Error al consultar el historial de sincronizacion.", ex);
            }
        }

        public async Task SyncUsuariosDirectorioAsync(CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();
            var syncControl = new SyncControl { EntityName = UsuariosDirectorioSyncEntityName };

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var loginService = scope.ServiceProvider.GetRequiredService<ILoginService>();
                var db = scope.ServiceProvider.GetRequiredService<HojasDbContext>();
                var errorIncidentService = scope.ServiceProvider.GetRequiredService<IErrorIncidentService>();
                var catalogCache = scope.ServiceProvider.GetRequiredService<ICatalogCacheService>();

                var users = await loginService.GetDirectoryUsersForSyncAsync(token);
                if (users.Count == 0)
                {
                    throw new InvalidOperationException("Microsoft Graph devolvio cero usuarios; se bloquea la reconciliacion para evitar bajas masivas.");
                }

                var snapshot = DirectoryUserSyncConsolidator.BuildSnapshot(users);
                var usersToSynchronize = snapshot.HdrUsers;
                var consolidatedDuplicates = snapshot.ConsolidatedDuplicateAccounts;
                if (consolidatedDuplicates > 0)
                {
                    _logger.LogInformation(
                        "Sync usuarios Entra: se consolidaron {ConsolidatedDuplicates} cuentas duplicadas por mail; si existe una cuenta HDR, prevalece sobre las cuentas sin grupo HDR.",
                        consolidatedDuplicates);
                }

                var changed = 0;
                var created = 0;
                var updated = 0;
                var reactivated = 0;
                var inactivated = 0;
                var skipped = 0;
                var invalidIssues = 0;
                var sectorIssues = 0;
                var conflictIssues = 0;
                var activeIssueFingerprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var protectedEntraIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var existingRevisores = await db.REVISORES.AsNoTracking().ToListAsync(token);
                var existingSocios = await db.SOCIOS.AsNoTracking().ToListAsync(token);

                foreach (var candidate in usersToSynchronize)
                {
                    token.ThrowIfCancellationRequested();
                    var user = candidate.User;
                    var mail = DirectoryUserSyncFormatter.NormalizeMail(user.Mail);

                    var missingFields = GetMissingDirectoryFields(user, mail);
                    if (!string.IsNullOrWhiteSpace(missingFields))
                    {
                        skipped++;
                        invalidIssues++;
                        ProtectConflictingLocalIdentities(user, existingRevisores, existingSocios, protectedEntraIds);
                        activeIssueFingerprints.Add(await ReportDirectoryUserIssueAsync(errorIncidentService, "HDR-SYNC-USERS-INVALID-001", user, "El usuario HDR no tiene los datos obligatorios para sincronizarse.", missingFields, token));
                        continue;
                    }

                    var sector = await db.SECTORES.AsNoTracking()
                        .FirstOrDefaultAsync(item => item.Detalle == user.Department, token);
                    if (sector == null)
                    {
                        skipped++;
                        sectorIssues++;
                        ProtectConflictingLocalIdentities(user, existingRevisores, existingSocios, protectedEntraIds);
                        activeIssueFingerprints.Add(await ReportDirectoryUserIssueAsync(errorIncidentService, "HDR-SYNC-USERS-SECTOR-001", user, $"No existe un sector configurado para department '{user.Department}'.", user.Department, token));
                        continue;
                    }

                    var result = await ExecuteDirectoryUserSyncAsync(
                        db,
                        user.Id,
                        mail,
                        user.GivenName,
                        user.Surname,
                        sector.Nombre,
                        user.HighestGroup.Nivel,
                        DirectoryUserSyncFormatter.BuildDetail(user.Surname, user.GivenName),
                        true,
                        candidate.AllowMailIdentityReassignment,
                        token);

                    if (result.IsConflict)
                    {
                        skipped++;
                        conflictIssues++;
                        var conflictingIds = ProtectConflictingLocalIdentities(user, existingRevisores, existingSocios, protectedEntraIds);
                        activeIssueFingerprints.Add(await ReportDirectoryUserIssueAsync(errorIncidentService, "HDR-SYNC-USERS-CONFLICT-001", user, "El id de Entra y el mail identifican registros diferentes; no se aplicaron cambios.", $"{result.Detail}; IdsExistentes={string.Join(',', conflictingIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase))}", token));
                    }
                    else
                    {
                        changed += result.Changes;
                        created += CountOutcomeActions(result.Detail, "Creado");
                        updated += CountOutcomeActions(result.Detail, "Actualizado");
                        reactivated += CountOutcomeActions(result.Detail, "Reactivado");
                        inactivated += CountOutcomeActions(result.Detail, "Inactivado");
                        LogDirectoryUserChange(user, mail, sector.Nombre, user.HighestGroup.Nivel, DirectoryUserSyncFormatter.BuildDetail(user.Surname, user.GivenName), user.HighestGroup.Name, result);
                    }
                }

                var resolvedIssues = await errorIncidentService.ResolveOpenIncidentsAsync("SyncUsuariosDirectorio", "HDR-SYNC-USERS-", activeIssueFingerprints, token);
                if (resolvedIssues > 0)
                {
                    _logger.LogInformation("Sync usuarios Entra resolvio incidencias persistentes. Count={ResolvedIssues}", resolvedIssues);
                }

                var inactivatedWithoutHdr = await InactivateDirectoryUsersWithoutHdrAsync(db, usersToSynchronize, protectedEntraIds, token);
                inactivated += inactivatedWithoutHdr;
                changed += inactivatedWithoutHdr;
                if (changed > 0)
                {
                    await catalogCache.InvalidateUsuariosAsync(token);
                }

                stopwatch.Stop();
                syncControl.LastSyncDate = DateTime.UtcNow;
                syncControl.Result = $"Sync usuarios: leidos={users.Count}; consolidados={consolidatedDuplicates}; hdr={snapshot.RawHdrCount}/{usersToSynchronize.Count}; altas={created}; actualizados={updated}; reactivados={reactivated}; inactivados={inactivated}; omitidos={skipped}; incidencias=invalidas:{invalidIssues},sector:{sectorIssues},conflictos:{conflictIssues}";
                await CreateSyncControl(syncControl);
                _logger.LogInformation("Sincronizacion de usuarios Entra finalizada. Total={Total}; Consolidados={ConsolidatedDuplicates}; HdrRaw={HdrRaw}; HdrLogicos={HdrLogical}; Altas={Created}; Actualizados={Updated}; Reactivados={Reactivated}; Inactivados={Inactivated}; Cambios={Changed}; Omitidos={Skipped}; IncidenciasInvalidas={InvalidIssues}; IncidenciasSector={SectorIssues}; IncidenciasConflicto={ConflictIssues}; DurationMs={DurationMs}", users.Count, consolidatedDuplicates, snapshot.RawHdrCount, usersToSynchronize.Count, created, updated, reactivated, inactivated, changed, skipped, invalidIssues, sectorIssues, conflictIssues, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                syncControl.LastSyncDate = DateTime.UtcNow;
                syncControl.Result = $"Error sync usuarios: {TruncateSyncResult(ex.Message, 70)}";
                await CreateSyncControl(syncControl);
                await ReportJobErrorAsync(ex, "HDR-SYNC-USERS-001", "La sincronizacion nocturna de usuarios no pudo completarse.", "SyncUsuariosDirectorio", token);
                _logger.LogError(ex, "Falla general en SyncUsuariosDirectorio. DurationMs={DurationMs}", stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        public async Task CreateSyncControl(SyncControl syncControl)
        {
            var fullResult = syncControl.Result ?? string.Empty;
            syncControl.Result = TruncateSyncControlResult(fullResult);

            if (fullResult.Length > SyncControlResultMaxLength)
            {
                _logger.LogWarning(
                    "El resultado de SyncControl para {EntityName} excede {MaxLength} caracteres y se truncara. Result={Result}",
                    syncControl.EntityName,
                    SyncControlResultMaxLength,
                    fullResult);
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var syncControlRepository = scope.ServiceProvider.GetRequiredService<IGenericRepository<SyncControl>>();
                await syncControlRepository.AddAsync(syncControl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al persistir registro de SyncControl para {EntityName}. Result={Result}", syncControl.EntityName, fullResult);
            }
        }

        private async Task<DirectoryUserSyncOutcome> ExecuteDirectoryUserSyncAsync(
            HojasDbContext db,
            string? entraObjectId,
            string? mail,
            string? givenName,
            string? surname,
            string? area,
            int? nivel,
            string? detalle,
            bool esHdr,
            bool permitirReasignacionPorMail,
            CancellationToken token)
        {
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(token);
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "dbo.sp_sync_usuario_directorio";
                command.CommandType = CommandType.StoredProcedure;
                AddParameter(command, "entra_object_id", entraObjectId);
                AddParameter(command, "mail", mail);
                AddParameter(command, "given_name", givenName);
                AddParameter(command, "surname", surname);
                AddParameter(command, "area", area);
                AddParameter(command, "nivel", nivel);
                AddParameter(command, "detalle", detalle);
                AddParameter(command, "es_hdr", esHdr);
                AddParameter(command, "permitir_reasignacion_por_mail", permitirReasignacionPorMail);
                var outcomeParameter = command.CreateParameter();
                outcomeParameter.ParameterName = "@resultado_detalle";
                outcomeParameter.Direction = ParameterDirection.Output;
                outcomeParameter.DbType = DbType.String;
                outcomeParameter.Size = 500;
                command.Parameters.Add(outcomeParameter);
                var returnValue = command.CreateParameter();
                returnValue.ParameterName = "@ReturnValue";
                returnValue.Direction = ParameterDirection.ReturnValue;
                returnValue.DbType = DbType.Int32;
                command.Parameters.Add(returnValue);
                await command.ExecuteNonQueryAsync(token);

                var result = returnValue.Value == DBNull.Value ? 0 : Convert.ToInt32(returnValue.Value);
                return new DirectoryUserSyncOutcome(
                    result < 0 ? 0 : result,
                    result == -3,
                    outcomeParameter.Value == DBNull.Value ? string.Empty : outcomeParameter.Value?.ToString() ?? string.Empty);
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private async Task<int> InactivateDirectoryUsersWithoutHdrAsync(
            HojasDbContext db,
            IReadOnlyList<DirectoryUserSyncCandidate> hdrUsers,
            IReadOnlySet<string> protectedEntraIds,
            CancellationToken token)
        {
            var identities = new DataTable();
            identities.Columns.Add("EntraObjectId", typeof(string));
            identities.Columns.Add("Mail", typeof(string));
            identities.Columns.Add("AllowMailFallback", typeof(bool));
            foreach (var candidate in hdrUsers)
            {
                var user = candidate.User;
                var mail = DirectoryUserSyncFormatter.NormalizeMail(user.Mail);
                if (!string.IsNullOrWhiteSpace(user.Id) || !string.IsNullOrWhiteSpace(mail))
                {
                    identities.Rows.Add(user.Id?.Trim(), mail, true);
                }
            }

            foreach (var protectedEntraId in protectedEntraIds)
            {
                identities.Rows.Add(protectedEntraId, DBNull.Value, false);
            }

            var hdrEntraIds = hdrUsers
                .Select(candidate => candidate.User.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var protectedIds = protectedEntraIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var hdrMails = hdrUsers
                .Select(candidate => DirectoryUserSyncFormatter.NormalizeMail(candidate.User.Mail))
                .Where(mail => !string.IsNullOrWhiteSpace(mail))
                .Select(mail => mail!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var revisoresAusentes = (await db.REVISORES.AsNoTracking().Where(revisor => revisor.Hdr_Activo).ToListAsync(token))
                .Where(revisor => !IsHdrOrProtectedIdentity(revisor.EntraObjectId, revisor.Mail, hdrEntraIds, hdrMails, protectedIds))
                .ToList();
            var sociosAusentes = (await db.SOCIOS.AsNoTracking().Where(socio => socio.Hdr_Activo).ToListAsync(token))
                .Where(socio => !IsHdrOrProtectedIdentity(socio.EntraObjectId, socio.Mail, hdrEntraIds, hdrMails, protectedIds))
                .ToList();

            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(token);
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "dbo.sp_inactivar_usuarios_directorio_sin_hdr";
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@usuarios_hdr", SqlDbType.Structured)
                {
                    TypeName = "dbo.HDR_DirectoryReconciliationIdentity",
                    Value = identities
                });
                var returnValue = command.CreateParameter();
                returnValue.ParameterName = "@ReturnValue";
                returnValue.Direction = ParameterDirection.ReturnValue;
                returnValue.DbType = DbType.Int32;
                command.Parameters.Add(returnValue);
                await command.ExecuteNonQueryAsync(token);
                var changes = returnValue.Value == DBNull.Value ? 0 : Convert.ToInt32(returnValue.Value);
                if (changes > 0)
                {
                    foreach (var revisor in revisoresAusentes)
                    {
                        _logger.LogInformation("Sync usuario Entra inactivado por no pertenecer a HDR. Tabla=REVISORES; EntraObjectId={EntraObjectId}; Mail={Mail}; Empleado={Empleado}; Detalle={Detalle}; Cargo={Cargo}; Area={Area}; Subarea={Subarea}", revisor.EntraObjectId, revisor.Mail, revisor.Empleado, revisor.Detalle, revisor.Cargo, revisor.Area, revisor.Subarea);
                    }

                    foreach (var socio in sociosAusentes)
                    {
                        _logger.LogInformation("Sync usuario Entra inactivado por no pertenecer a HDR. Tabla=SOCIOS; EntraObjectId={EntraObjectId}; Mail={Mail}; Socio={Socio}; Detalle={Detalle}; LiderDeArea={LiderDeArea}", socio.EntraObjectId, socio.Mail, socio.Socio, socio.Detalle, socio.LiderDeArea);
                    }
                }

                return changes;
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private static bool IsHdrOrProtectedIdentity(
            string? entraObjectId,
            string? mail,
            IReadOnlySet<string> hdrEntraIds,
            IReadOnlySet<string> hdrMails,
            IReadOnlySet<string> protectedEntraIds) =>
            !string.IsNullOrWhiteSpace(entraObjectId)
                ? hdrEntraIds.Contains(entraObjectId) || protectedEntraIds.Contains(entraObjectId)
                : hdrMails.Contains(mail ?? string.Empty);

        private static IReadOnlyCollection<string> ProtectConflictingLocalIdentities(
            DirectoryUserSyncRecord user,
            IReadOnlyList<Revisores> revisores,
            IReadOnlyList<Socios> socios,
            ISet<string> protectedEntraIds)
        {
            var mail = DirectoryUserSyncFormatter.NormalizeMail(user.Mail);
            if (string.IsNullOrWhiteSpace(mail))
            {
                return Array.Empty<string>();
            }

            var conflictingIds = revisores.Select(item => new { item.EntraObjectId, item.Mail })
                .Concat(socios.Select(item => new { item.EntraObjectId, item.Mail }))
                .Where(item => !string.IsNullOrWhiteSpace(item.EntraObjectId) &&
                               !string.Equals(item.EntraObjectId, user.Id, StringComparison.OrdinalIgnoreCase) &&
                               string.Equals(DirectoryUserSyncFormatter.NormalizeMail(item.Mail), mail, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.EntraObjectId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var entraObjectId in conflictingIds)
            {
                protectedEntraIds.Add(entraObjectId);
            }

            return conflictingIds;
        }

        private static string GetMissingDirectoryFields(DirectoryUserSyncRecord user, string? normalizedMail)
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(user.Id)) missing.Add("id");
            if (string.IsNullOrWhiteSpace(normalizedMail)) missing.Add("mail");
            if (string.IsNullOrWhiteSpace(user.GivenName)) missing.Add("givenName");
            if (string.IsNullOrWhiteSpace(user.Surname)) missing.Add("surname");
            if (string.IsNullOrWhiteSpace(user.Department)) missing.Add("department");
            return string.Join(',', missing);
        }

        private async Task<string> ReportDirectoryUserIssueAsync(
            IErrorIncidentService errorIncidentService,
            string errorCode,
            DirectoryUserSyncRecord user,
            string message,
            string? cause,
            CancellationToken token)
        {
            var identity = user.Id ?? user.Mail ?? "(sin id ni mail)";
            var fingerprint = CreateDirectoryIssueFingerprint(errorCode, user.Id, DirectoryUserSyncFormatter.NormalizeMail(user.Mail), cause);
            var result = await errorIncidentService.ReportOnceAsync(
                new InvalidOperationException($"{message} Usuario={identity}"),
                errorCode,
                message,
                new ErrorIncidentContext { Endpoint = "SyncUsuariosDirectorio", UserName = user.Mail, Fingerprint = fingerprint },
                token);
            if (!result.Created)
            {
                _logger.LogDebug("Incidencia de sincronizacion Entra ya abierta. ErrorCode={ErrorCode}; Usuario={Usuario}; IncidentId={IncidentId}", errorCode, identity, result.IncidentId);
            }

            return fingerprint;
        }

        private static string CreateDirectoryIssueFingerprint(string errorCode, string? entraObjectId, string? mail, string? cause)
        {
            var value = string.Join('|', errorCode, entraObjectId?.Trim().ToUpperInvariant(), mail?.Trim().ToUpperInvariant(), cause?.Trim().ToUpperInvariant());
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        }

        private static int CountOutcomeActions(string detail, string actionFragment) =>
            detail.Split(';', StringSplitOptions.RemoveEmptyEntries).Count(action => action.Contains(actionFragment, StringComparison.Ordinal));

        private static void AddParameter(System.Data.Common.DbCommand command, string name, object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@" + name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private void LogDirectoryUserChange(
            DirectoryUserSyncRecord user,
            string? mail,
            string? area,
            int? nivel,
            string? detalle,
            string? groupName,
            DirectoryUserSyncOutcome outcome)
        {
            if (outcome.Changes == 0 || outcome.IsConflict)
            {
                return;
            }

            _logger.LogInformation(
                "Sync usuario Entra con cambios. Resultado={Resultado}; EntraObjectId={EntraObjectId}; Mail={Mail}; GivenName={GivenName}; Surname={Surname}; Department={Department}; Area={Area}; Nivel={Nivel}; Grupo={Grupo}; Detalle={Detalle}",
                outcome.Detail,
                user.Id,
                mail,
                user.GivenName,
                user.Surname,
                user.Department,
                area,
                nivel,
                groupName,
                detalle);
        }

        private async Task<bool> HasHistoricalReferencesAsync(HojasDbContext db, int clientId, CancellationToken token)
        {
            return await db.Hojas
                .AsNoTracking()
                .AnyAsync(hoja => hoja.Cliente == clientId, token);
        }

        private static string BuildClientSyncSummary(
            int remoteCount,
            int localCount,
            int insertedCount,
            int reactivatedCount,
            int candidateInactivationCount,
            int confirmedInactivationCount,
            int inactivatedCount,
            int skippedCount,
            string reason)
        {
            var summary = new StringBuilder();
            summary.Append("Sync clientes: ");
            summary.Append($"remoteSincronizables={remoteCount}; ");
            summary.Append($"locales={localCount}; ");
            summary.Append($"insertados={insertedCount}; ");
            summary.Append($"reactivados={reactivatedCount}; ");
            summary.Append($"candidatosInactivacion={candidateInactivationCount}; ");
            summary.Append($"confirmadosInactivacion={confirmedInactivationCount}; ");
            summary.Append($"inactivados={inactivatedCount}; ");
            summary.Append($"omitidos={skippedCount}");

            if (!string.IsNullOrWhiteSpace(reason))
            {
                summary.Append($"; detalle={reason}");
            }

            return summary.ToString();
        }

        private async Task ExecuteDailySyncAsync(CancellationToken stoppingToken, DateTime scheduledAt)
        {
            _logger.LogInformation("Comienzo de ejecucion diaria pendiente/programada. ScheduledAt={ScheduledAt}", scheduledAt);

            try
            {
                await SyncContacts(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falla critica en la tarea diaria SyncContacts.");
            }

            try
            {
                await SyncUsuariosDirectorioAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falla critica en la tarea diaria SyncUsuariosDirectorio.");
            }

            try
            {
                await SyncContratos(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falla critica en la tarea diaria SyncContratos.");
            }
        }

        private async Task ExecuteWeeklyNotificationAsync(CancellationToken stoppingToken, DateTime scheduledAt)
        {
            _logger.LogInformation("Comienzo de ejecucion semanal pendiente/programada. ScheduledAt={ScheduledAt}", scheduledAt);
            try
            {
                await NotificacionHojasPendientes(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falla critica en la tarea semanal NotificacionHojasPendientes.");
            }
            finally
            {
                await CleanupErrorLogsAsync(stoppingToken);
            }
        }

        private async Task CleanupErrorLogsAsync(CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var errorIncidentService = scope.ServiceProvider.GetRequiredService<IErrorIncidentService>();
                var cutoffUtc = DateTime.UtcNow.AddDays(-30);
                var deleted = await errorIncidentService.DeleteExpiredAsync(cutoffUtc, token);
                stopwatch.Stop();
                _logger.LogInformation("Limpieza semanal de ErrorLog finalizada. CutoffUtc={CutoffUtc} Deleted={Deleted} DurationMs={DurationMs}", cutoffUtc, deleted, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "HDR-ERRORLOG-CLEANUP-001 Error al limpiar ErrorLog. DurationMs={DurationMs}", stopwatch.ElapsedMilliseconds);
            }
        }

        private async Task ReportJobErrorAsync(Exception exception, string errorCode, string userMessage, string jobName, CancellationToken token)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var errorIncidentService = scope.ServiceProvider.GetRequiredService<IErrorIncidentService>();
                await errorIncidentService.ReportAsync(exception, errorCode, userMessage, new ErrorIncidentContext { Endpoint = jobName }, token);
            }
            catch (Exception reportException)
            {
                _logger.LogCritical(reportException, "No se pudo registrar la incidencia del job {JobName}. ErrorCode={ErrorCode}", jobName, errorCode);
            }
        }

        private DateTime GetDailyScheduledTime(DateTime reference)
        {
            return new DateTime(
                reference.Year,
                reference.Month,
                reference.Day,
                _syncSettings.SyncClientesRunHour,
                _syncSettings.SyncClientesRunMinute,
                0);
        }

        private DateTime GetWeeklyScheduledTime(DateTime reference)
        {
            var weeklyDay = Enum.Parse<DayOfWeek>(_syncSettings.NotificacionSemanalDay);
            var daysUntilWeekly = ((int)weeklyDay - (int)reference.DayOfWeek + 7) % 7;

            return new DateTime(
                reference.Year,
                reference.Month,
                reference.Day,
                _syncSettings.NotificacionSemanalHour,
                _syncSettings.NotificacionSemanalMinute,
                0).AddDays(daysUntilWeekly);
        }

        private async Task<DateTime?> GetLastSuccessfulOrRecordedSyncOrNull(string entityNameValue)
        {
            var lastSync = await GetLastSync(entityNameValue);
            return lastSync == DateTime.MinValue ? null : lastSync;
        }

        private static string? ReadTrimmedString(SqlDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return reader.GetValue(ordinal)?.ToString()?.Trim();
        }

        private void AddContratoRow(
            List<Contratos> resultado,
            string? codigoPlataforma,
            string? razonSocial,
            string? contrato,
            string? fechaAltaRaw,
            ref int fechasValidas,
            ref int fechasInvalidasONulas)
        {
            if (string.IsNullOrWhiteSpace(codigoPlataforma) || string.IsNullOrWhiteSpace(contrato))
            {
                return;
            }

            DateTime? fechaAlta = TryParseFechaAlta(fechaAltaRaw, out var parsedFechaAlta)
                ? parsedFechaAlta
                : null;

            if (fechaAlta.HasValue)
            {
                fechasValidas++;
            }
            else
            {
                fechasInvalidasONulas++;
            }

            resultado.Add(new Contratos
            {
                CodigoPlataforma = codigoPlataforma,
                RazonSocial = razonSocial,
                Contrato = contrato,
                FechaAlta = fechaAlta
            });
        }

        private static bool TryParseFechaAlta(string? value, out DateTime parsedDate)
        {
            parsedDate = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var acceptedFormats = new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" };
            return DateTime.TryParseExact(
                       value.Trim(),
                       acceptedFormats,
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.None,
                       out parsedDate)
                   || DateTime.TryParse(
                       value.Trim(),
                       new CultureInfo("es-AR"),
                       DateTimeStyles.None,
                       out parsedDate);
        }

        private static string BuildSqlConnectionDiagnostic(string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return
                $"DataSource={builder.DataSource}; InitialCatalog={builder.InitialCatalog}; UserID={builder.UserID}; " +
                $"IntegratedSecurity={builder.IntegratedSecurity}; Encrypt={builder.Encrypt}; " +
                $"TrustServerCertificate={builder.TrustServerCertificate}; MultipleActiveResultSets={builder.MultipleActiveResultSets}; " +
                $"ConnectTimeout={builder.ConnectTimeout}";
        }

        private static string BuildExceptionSummary(Exception ex)
        {
            var parts = new List<string>();
            var current = ex;
            var depth = 0;

            while (current != null && depth < 4)
            {
                parts.Add($"{current.GetType().Name}: {current.Message}");
                current = current.InnerException;
                depth++;
            }

            return string.Join(" | ", parts);
        }

        private static bool IsTlsHandshakeFailure(Exception ex)
        {
            return BuildExceptionSummary(ex).Contains("pre-login handshake", StringComparison.OrdinalIgnoreCase)
                || BuildExceptionSummary(ex).Contains("SSL Provider", StringComparison.OrdinalIgnoreCase)
                || BuildExceptionSummary(ex).Contains("Encryption(ssl/tls) handshake failed", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<List<SqlCmdContratoRow>> ObtenerContratosDesdeVistaConSqlCmd(
            string connectionString,
            string query,
            CancellationToken token)
        {
            var executablePath = ResolveSqlCmdPath();
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new InvalidOperationException(
                    "No se encontro sqlcmd en el contenedor para ejecutar el fallback de contratos.");
            }

            var builder = new SqlConnectionStringBuilder(connectionString);
            var isSqlCmd18 = executablePath.Contains("mssql-tools18", StringComparison.OrdinalIgnoreCase);
            var processStartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            processStartInfo.ArgumentList.Add("-S");
            processStartInfo.ArgumentList.Add(NormalizeSqlCmdServer(builder.DataSource));
            processStartInfo.ArgumentList.Add("-d");
            processStartInfo.ArgumentList.Add(builder.InitialCatalog);
            processStartInfo.ArgumentList.Add("-U");
            processStartInfo.ArgumentList.Add(builder.UserID);
            if (isSqlCmd18)
            {
                processStartInfo.ArgumentList.Add(builder.Encrypt ? "-Nm" : "-No");
                if (builder.TrustServerCertificate)
                {
                    processStartInfo.ArgumentList.Add("-C");
                }
            }
            processStartInfo.ArgumentList.Add("-h");
            processStartInfo.ArgumentList.Add("-1");
            processStartInfo.ArgumentList.Add("-s");
            processStartInfo.ArgumentList.Add("\t");
            processStartInfo.ArgumentList.Add("-W");
            processStartInfo.ArgumentList.Add("-Q");
            processStartInfo.ArgumentList.Add(query);
            processStartInfo.ArgumentList.Add("-l");
            processStartInfo.ArgumentList.Add(Math.Max(builder.ConnectTimeout, 15).ToString(CultureInfo.InvariantCulture));

            processStartInfo.Environment["SQLCMDPASSWORD"] = builder.Password;

            _logger.LogInformation(
                "Ejecutando fallback sqlcmd para contratos. Executable={Executable}; Server={Server}; Database={Database}; User={User}",
                executablePath,
                NormalizeSqlCmdServer(builder.DataSource),
                builder.InitialCatalog,
                builder.UserID);

            using var process = new Process { StartInfo = processStartInfo };

            if (!process.Start())
            {
                throw new InvalidOperationException("No se pudo iniciar el proceso sqlcmd para contratos.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(token);
            var stderrTask = process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"sqlcmd finalizo con codigo {process.ExitCode}. stderr={TruncateSyncResult(stderr, 500)}");
            }

            _logger.LogInformation(
                "Fallback sqlcmd ejecutado correctamente. StdoutLength={StdoutLength}; StderrLength={StderrLength}",
                stdout.Length,
                stderr.Length);

            return ParseSqlCmdContratos(stdout);
        }

        private static string? ResolveSqlCmdPath()
        {
            foreach (var candidatePath in SqlCmdCandidatePaths)
            {
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            return null;
        }

        private static string NormalizeSqlCmdServer(string dataSource)
        {
            if (dataSource.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            {
                return dataSource;
            }

            return $"tcp:{dataSource}";
        }

        private List<SqlCmdContratoRow> ParseSqlCmdContratos(string stdout)
        {
            var rows = new List<SqlCmdContratoRow>();
            var lines = Regex.Split(stdout ?? string.Empty, "\r?\n");

            foreach (var rawLine in lines)
            {
                var line = rawLine?.TrimEnd();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var parts = line.Split('\t');
                if (parts.Length < 4)
                {
                    _logger.LogWarning("Se omite una fila de sqlcmd por formato inesperado. RawLine={RawLine}", line);
                    continue;
                }

                rows.Add(new SqlCmdContratoRow(
                    parts[0].Trim(),
                    parts[1].Trim(),
                    parts[2].Trim(),
                    parts[3].Trim()));
            }

            _logger.LogInformation("Fallback sqlcmd parseado correctamente. Count={Count}", rows.Count);
            return rows;
        }

        internal static string TruncateSyncControlResult(string? value)
        {
            return TruncateSyncResult(value ?? string.Empty, SyncControlResultMaxLength);
        }

        private static string TruncateSyncResult(string value, int maxLength = 900)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value[..maxLength];
        }

        private sealed record ContratosRemoteLoadResult(
            List<Contratos> Contratos,
            int TotalLeidos,
            int FechasValidas,
            int FechasInvalidasONulas);

        private sealed record SqlCmdContratoRow(
            string CodigoPlataforma,
            string RazonSocial,
            string Contrato,
            string FechaAltaRaw);

        private sealed record DirectoryUserSyncOutcome(int Changes, bool IsConflict, string Detail);
    }
}
