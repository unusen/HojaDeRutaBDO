using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;
using HojaDeRuta.DBContext;
using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;
using HojaDeRuta.Services.Repository;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HojaDeRuta.Services
{
    public class SyncService : BackgroundService
    {
        private const string ClientesSyncEntityName = "Clientes_Creatio";
        private readonly ClientSyncReconciler _clientSyncReconciler = new();
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
                var now = DateTime.Now;
                _logger.LogInformation("Ejecucion del servicio de sincronizacion a las {Time}", now);

                var nextRunDaily = new DateTime(now.Year, now.Month, now.Day, _syncSettings.SyncClientesRunHour, _syncSettings.SyncClientesRunMinute, 0);
                if (now > nextRunDaily)
                {
                    nextRunDaily = nextRunDaily.AddDays(1);
                }

                var weeklyDay = Enum.Parse<DayOfWeek>(_syncSettings.NotificacionSemanalDay);
                var daysUntilWeekly = ((int)weeklyDay - (int)now.DayOfWeek + 7) % 7;
                if (daysUntilWeekly == 0 && now > nextRunDaily)
                {
                    daysUntilWeekly = 7;
                }

                var nextRunWeekly = new DateTime(now.Year, now.Month, now.Day, _syncSettings.NotificacionSemanalHour, _syncSettings.NotificacionSemanalMinute, 0)
                    .AddDays(daysUntilWeekly);

                if (nextRunWeekly <= now)
                {
                    nextRunWeekly = nextRunWeekly.AddDays(7);
                }

                var nextRun = nextRunDaily < nextRunWeekly ? nextRunDaily : nextRunWeekly;
                var timeUntilNextRun = nextRun - now;

                _logger.LogInformation("Proxima ejecucion diaria: {DailyRun}. Proxima ejecucion semanal: {WeeklyRun}", nextRunDaily, nextRunWeekly);

                await Task.Delay(timeUntilNextRun, stoppingToken);

                if (DateTime.Now >= nextRunDaily && DateTime.Now < nextRunDaily.AddMinutes(1))
                {
                    _logger.LogInformation("Comienzo de ejecucion diaria (Clientes y Contratos)");

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
                        await SyncContratos(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Falla critica en la tarea diaria SyncContratos.");
                    }
                }

                if (DateTime.Now >= nextRunWeekly && DateTime.Now < nextRunWeekly.AddMinutes(1))
                {
                    _logger.LogInformation("Comienzo de ejecucion semanal (Notificaciones)");
                    try
                    {
                        await NotificacionHojasPendientes(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Falla critica en la tarea semanal NotificacionHojasPendientes.");
                    }
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
                _logger.LogInformation("Inicio de reconciliacion completa de clientes activos con Creatio.");

                var clientesCreatio = creatioService.GetClientesActivos();
                var clientesLocales = await db.Clientes_Creatio
                    .AsNoTracking()
                    .OrderBy(cliente => cliente.Id)
                    .ToListAsync(token);

                _logger.LogInformation(
                    "Clientes activos obtenidos desde Creatio. RemoteCount={RemoteCount} LocalCount={LocalCount}",
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
                        candidateDeletionCount: 0,
                        confirmedDeletionCount: 0,
                        deletedCount: 0,
                        skippedCount: 0,
                        reason: "Se bloqueo la reconciliacion porque Creatio devolvio 0 clientes activos.");

                    _logger.LogWarning(syncControl.Result);
                    await CreateSyncControl(syncControl);
                    return;
                }

                var plan = _clientSyncReconciler.BuildPlan(clientesCreatio, clientesLocales);
                var shouldBlockDeletions = _clientSyncReconciler.ShouldBlockDeletions(plan.RemoteActiveCount, plan.LocalCount);
                var insertedCount = 0;
                var confirmedDeletionCount = 0;
                var deletedCount = 0;
                var skippedCount = 0;
                const int reactivatedCount = 0;

                if (plan.ClientsToInsert.Any())
                {
                    _logger.LogInformation("Clientes nuevos detectados para insertar. Count={Count}", plan.ClientsToInsert.Count);
                    await db.Clientes_Creatio.AddRangeAsync(plan.ClientsToInsert, token);
                    insertedCount = await db.SaveChangesAsync(token);
                    db.ChangeTracker.Clear();
                }

                var candidatesToDelete = plan.ClientsCandidateToDelete;
                var confirmedCandidates = new List<Clientes>();

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
                                skippedCount++;
                                _logger.LogWarning(
                                    "Se omite la baja del cliente {ClientId}/{CodigoPlataforma} porque tiene hojas historicas asociadas.",
                                    candidate.Id,
                                    candidate.CodigoPlataforma);
                                continue;
                            }

                            try
                            {
                                var activeInCreatio = creatioService.GetClienteActivoByCodigoPlataforma(candidate.CodigoPlataforma);
                                if (activeInCreatio != null)
                                {
                                    skippedCount++;
                                    _logger.LogInformation(
                                        "Doble check de baja descartado para cliente {ClientId}/{CodigoPlataforma}: continua activo en Creatio.",
                                        candidate.Id,
                                        candidate.CodigoPlataforma);
                                    continue;
                                }

                                confirmedCandidates.Add(candidate);
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

                confirmedDeletionCount = confirmedCandidates.Count;
                if (confirmedCandidates.Any())
                {
                    var idsToDelete = confirmedCandidates.Select(cliente => cliente.Id).ToList();
                    var trackedClientsToDelete = await db.Clientes_Creatio
                        .Where(cliente => idsToDelete.Contains(cliente.Id))
                        .ToListAsync(token);

                    db.Clientes_Creatio.RemoveRange(trackedClientsToDelete);
                    deletedCount = await db.SaveChangesAsync(token);
                    db.ChangeTracker.Clear();
                }

                if (insertedCount > 0 || deletedCount > 0)
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
                    candidateDeletionCount: candidatesToDelete.Count,
                    confirmedDeletionCount: confirmedDeletionCount,
                    deletedCount: deletedCount,
                    skippedCount: skippedCount,
                    reason: $"duracionMs={stopwatch.ElapsedMilliseconds}");

                _logger.LogInformation(syncControl.Result);
                await CreateSyncControl(syncControl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la sincronizacion de clientes con Creatio.");
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
                syncControl.Result = "Error en el envio de notificaciones semanales.";
                await CreateSyncControl(syncControl);
            }
        }

        public async Task SyncContratos(CancellationToken token)
        {
            _logger.LogInformation("Comienzo de SyncContratos");

            var syncControl = new SyncControl
            {
                EntityName = "contratos_completo",
                LastSyncDate = DateTime.UtcNow
            };

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HojasDbContext>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var remoteConnectionString = configuration.GetConnectionString("vistaContratos");

            if (string.IsNullOrWhiteSpace(remoteConnectionString))
            {
                throw new InvalidOperationException("Falta configurar ConnectionStrings:vistaContratos.");
            }

            try
            {
                var vistaContratos = await ObtenerContratosDesdeVista(remoteConnectionString, token);
                _logger.LogInformation("Se obtuvieron {Count} contratos desde la vista remota.", vistaContratos.Count);

                if (vistaContratos.Count == 0)
                {
                    syncControl.Result = "La vista remota no devolvio contratos. No se realizaron cambios.";
                    _logger.LogWarning(syncControl.Result);
                    await CreateSyncControl(syncControl);
                    return;
                }

                await using var transaction = await db.Database.BeginTransactionAsync(token);

                try
                {
                    await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE CONTRATOS_COMPLETO", token);
                    _logger.LogInformation("Tabla CONTRATOS_COMPLETO truncada correctamente.");

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

                    syncControl.Result = $"Sync completo: se insertaron {totalInsertados} contratos (reemplazo total).";
                    _logger.LogInformation(syncControl.Result);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(token);
                    _logger.LogError(ex, "Error durante truncate/insert. Se hizo rollback.");
                    throw;
                }

                await CreateSyncControl(syncControl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falla general en SyncContratos.");
                syncControl.Result = "Error al sincronizar contratos con la vista remota.";
                await CreateSyncControl(syncControl);
            }
        }

        private async Task<List<Contratos>> ObtenerContratosDesdeVista(string connectionString, CancellationToken token)
        {
            var resultado = new List<Contratos>();

            using var remoteConn = new SqlConnection(connectionString);
            await remoteConn.OpenAsync(token);

            const string query = @"
                SELECT CodigoPlataforma, Contrato
                FROM CONTRATOS_COMPLETO
                WHERE Id IS NOT NULL
                  AND CodigoPlataforma IS NOT NULL
                  AND LTRIM(RTRIM(CodigoPlataforma)) <> ''
                  AND Contrato IS NOT NULL
                  AND LTRIM(RTRIM(Contrato)) <> ''";

            using var cmd = new SqlCommand(query, remoteConn) { CommandTimeout = 120 };
            using var reader = await cmd.ExecuteReaderAsync(token);

            int colCodigo = reader.GetOrdinal("CodigoPlataforma");
            int colContrato = reader.GetOrdinal("Contrato");

            while (await reader.ReadAsync(token))
            {
                resultado.Add(new Contratos
                {
                    CodigoPlataforma = reader.GetString(colCodigo),
                    Contrato = reader.GetString(colContrato)
                });
            }

            _logger.LogInformation("Lectura de vista remota finalizada: {Count} registros.", resultado.Count);
            return resultado;
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

        public async Task CreateSyncControl(SyncControl syncControl)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var syncControlRepository = scope.ServiceProvider.GetRequiredService<IGenericRepository<SyncControl>>();
                await syncControlRepository.AddAsync(syncControl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al persistir registro de SyncControl para {EntityName}", syncControl.EntityName);
                throw new Exception("Error al guardar el control de sincronizacion.", ex);
            }
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
            int candidateDeletionCount,
            int confirmedDeletionCount,
            int deletedCount,
            int skippedCount,
            string reason)
        {
            var summary = new StringBuilder();
            summary.Append("Sync clientes: ");
            summary.Append($"remoteActivos={remoteCount}; ");
            summary.Append($"locales={localCount}; ");
            summary.Append($"insertados={insertedCount}; ");
            summary.Append($"reactivados={reactivatedCount}; ");
            summary.Append($"candidatosBaja={candidateDeletionCount}; ");
            summary.Append($"confirmadosBaja={confirmedDeletionCount}; ");
            summary.Append($"eliminados={deletedCount}; ");
            summary.Append($"omitidos={skippedCount}");

            if (!string.IsNullOrWhiteSpace(reason))
            {
                summary.Append($"; detalle={reason}");
            }

            return summary.ToString();
        }
    }
}
