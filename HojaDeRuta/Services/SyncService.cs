using HojaDeRuta.DBContext;
using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;
using HojaDeRuta.Services.Repository;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;

namespace HojaDeRuta.Services
{
    public class SyncService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly SyncSettings _syncSettings;

        public SyncService(
            IServiceProvider serviceProvider,
            IOptions<SyncSettings> options
            )
        {
            _serviceProvider = serviceProvider;
            _syncSettings = options.Value;
        }

        //protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        //{
        //    while (!stoppingToken.IsCancellationRequested)
        //    {
        //        var now = DateTime.Now;
        //        var nextRunTime = new DateTime(now.Year, now.Month, now.Day, _syncSettings.RunHour, _syncSettings.RunMinute, 0);

        //        if (now > nextRunTime)
        //        {
        //            nextRunTime = nextRunTime.AddDays(1);
        //        }

        //        var timeUntilNextRun = nextRunTime - now;
        //        await Task.Delay(timeUntilNextRun, stoppingToken);

        //        try
        //        {
        //            await SyncContacts(stoppingToken);
        //        }
        //        catch (Exception ex)
        //        {
        //            //_logger.LogError(ex, "Error durante la sincronización con Creatio");
        //        }

        //        await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        //    }
        //}

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;

                // --- Ejecución diaria ---
                var nextRunDaily = new DateTime(now.Year, now.Month, now.Day, _syncSettings.SyncClientesRunHour, _syncSettings.SyncClientesRunMinute, 0);
                if (now > nextRunDaily) nextRunDaily = nextRunDaily.AddDays(1);

                // --- Ejecución semanal ---
                var weeklyDay = Enum.Parse<DayOfWeek>(_syncSettings.NotificacionSemanalDay);
                var daysUntilWeekly = ((int)weeklyDay - (int)now.DayOfWeek + 7) % 7;
                if (daysUntilWeekly == 0 && now > nextRunDaily)
                    daysUntilWeekly = 7;

                var nextRunWeekly = new DateTime(now.Year, now.Month, now.Day,
                    _syncSettings.NotificacionSemanalHour, _syncSettings.NotificacionSemanalMinute, 0).AddDays(daysUntilWeekly);

                if (nextRunWeekly <= now)
                {
                    nextRunWeekly = nextRunWeekly.AddDays(7);
                }

                var nextRun = nextRunDaily < nextRunWeekly ? nextRunDaily : nextRunWeekly;
                var timeUntilNextRun = nextRun - now;

                await Task.Delay(timeUntilNextRun, stoppingToken);

                if (DateTime.Now >= nextRunDaily && DateTime.Now < nextRunDaily.AddMinutes(1))
                {
                    //try { await SyncContacts(stoppingToken); }
                    //    catch (Exception ex) { throw new Exception(ex.Message); }

                    try { await SyncContratos(stoppingToken); }
                    catch (Exception ex) { throw new Exception(ex.Message); }

                    //await SyncContacts(stoppingToken);
                }

                if (DateTime.Now >= nextRunWeekly && DateTime.Now < nextRunWeekly.AddMinutes(1))
                {
                    await NotificacionHojasPendientes(stoppingToken);
                }
            }
        }


        public async Task SyncContacts(CancellationToken token)
        {
            SyncControl syncControl = new SyncControl
            {
                EntityName = "Clientes_Creatio"
            };

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HojasDbContext>();

            var _creatioService = scope.ServiceProvider.GetRequiredService<CreatioService>();
            var _sharedService = scope.ServiceProvider.GetRequiredService<SharedService>();
            var _clienteService = scope.ServiceProvider.GetRequiredService<ClienteService>();

            try
            {
                DateTime? lastSync = await GetLastSync("Clientes_Creatio");
                //lastSync = new DateTime(2024, 09, 06, 15, 16, 0);

                List<Account> ClientesCreatio = _creatioService.GetClientesByCreatedOn(lastSync.Value);

                List<Clientes> clientes = new List<Clientes>();

                foreach (var item in ClientesCreatio)
                {
                    if (item.BGClienteID == 0)
                    {
                        continue;
                    }

                    var entity = await db.Clientes_Creatio
                        .FirstOrDefaultAsync(c => Convert.ToInt32(c.CodigoPlataforma) == item.BGClienteID, token);

                    if (entity == null)
                    {
                        Clientes cliente = new Clientes
                        {
                            RazonSocial = item.AlternativeName,
                            CodigoPlataforma = item.BGClienteID.ToString()
                        };

                        clientes.Add(cliente);
                    }
                }

                if (clientes.Count == 0)
                {
                    syncControl.LastSyncDate = DateTime.UtcNow;
                    syncControl.Result = "No se obtuvieron clientes para integrar";
                }
                else
                {
                    await _clienteService.CreateClientes(clientes);

                    syncControl.LastSyncDate = DateTime.UtcNow;

                    syncControl.Result = ClientesCreatio.Count == 1
                        ? $"Se integró {ClientesCreatio.Count} nuevo cliente"
                        : $"Se integraron {ClientesCreatio.Count} nuevos clientes";
                }

                await CreateSyncControl(syncControl);
            }
            catch (Exception ex)
            {
                syncControl.LastSyncDate = DateTime.UtcNow;
                syncControl.Result = $"Error al sincronizar clientes {ex.Message}";
                await CreateSyncControl(syncControl);
            }
        }

        public async Task NotificacionHojasPendientes(CancellationToken token)
        {
            SyncControl syncControl = new SyncControl
            {
                EntityName = "Email_Pendientes",
                LastSyncDate = DateTime.UtcNow
            };

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HojasDbContext>();

            var _mailService = scope.ServiceProvider.GetRequiredService<MailService>();
            var _sharedService = scope.ServiceProvider.GetRequiredService<SharedService>();
            var _hojasService = scope.ServiceProvider.GetRequiredService<HojaDeRutaService>();

            try
            {
                List<HojaPendiente> pendientes = await _hojasService.GetHojasPendientes();

                if (pendientes.Any())
                {
                    foreach (var pendiente in pendientes)
                    {
                        string hoja = pendientes.Count == 1 ? "hoja" : "hojas";

                        string subject = $"{pendiente.Revisor} tenés {pendiente.CantidadRegistros}" +
                            $" {hoja} de ruta sin revisar";

                        string body = await _mailService.GetBodyNotificacionSemanal(pendiente);

                        List<string> destinatarios = new List<string>
                        {
                            pendiente.Revisor
                        };

                        await _mailService.SendMailAsync(subject, destinatarios, body, true);
                    }

                    syncControl.Result = $"Se envió el correo semanal a {pendientes.Count} destinatarios.";
                }
                else
                {
                    syncControl.Result = $"No se encontraron hojas pendientes para el envio semanal.";
                }

                await CreateSyncControl(syncControl);
            }
            catch (Exception ex)
            {
                syncControl.Result = $"Error en envío semanal: {ex.Message}";
                await CreateSyncControl(syncControl);
            }
        }

        public async Task SyncContratos(CancellationToken token)
        {
            SyncControl syncControl = new SyncControl
            {
                EntityName = "contratos_completo"
            };

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HojasDbContext>();
            var _sharedService = scope.ServiceProvider.GetRequiredService<SharedService>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            var remoteConnectionString = configuration.GetConnectionString("vistaContratos");

            try
            {
                List<Contratos> vistaContratos = new List<Contratos>();

                //Obtener contratos de la vista
                try
                {
                    using (var remoteConn = new SqlConnection(remoteConnectionString))
                    {
                        await remoteConn.OpenAsync(token);

                        var query = "SELECT * FROM CONTRATOS_COMPLETO WHERE" +
                            " Id IS NOT NULL" +
                            " AND CodigoPlataforma IS NOT NULL AND LTRIM(RTRIM(CodigoPlataforma)) <> ''" +
                            " AND Contrato IS NOT NULL AND LTRIM(RTRIM(Contrato)) <> ''";

                        using (var cmd = new SqlCommand(query, remoteConn))
                        using (var reader = await cmd.ExecuteReaderAsync(token))
                        {
                            while (await reader.ReadAsync(token))
                            {
                                vistaContratos.Add(
                                    new Contratos {
                                        //Id = reader.GetInt32(0),
                                        CodigoPlataforma = reader.GetString(1),
                                        Contrato = reader.GetString(2)
                                    }
                                );
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }

                //Obtener nombres de contratos de la base HDR
                var contratosLocales = (await _sharedService.GetContratos())
                    .Select(c => c.Contrato)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct()
                    .ToHashSet();

                var nuevosContratos = vistaContratos
                    .Where(r => !contratosLocales.Contains(r.Contrato))
                    .ToList();

                if (nuevosContratos.Count == 0)
                {
                    syncControl.LastSyncDate = DateTime.UtcNow;
                    syncControl.Result = "No se encontraron nuevos contratos para insertar.";
                }
                else
                {
                    await _sharedService.CreateContratosRange(nuevosContratos);

                    syncControl.LastSyncDate = DateTime.UtcNow;
                    syncControl.Result = $"Se insertaron {nuevosContratos.Count} nuevos contratos.";
                }

                await CreateSyncControl(syncControl);
            }
            catch (Exception ex)
            {
                syncControl.Result = $"Error al sincronizar contratos: {ex.Message}";
                syncControl.LastSyncDate = DateTime.UtcNow;
                await CreateSyncControl(syncControl);
            }
        }

        public async Task<DateTime?> GetLastSync(string EntityName)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<HojasDbContext>();
                var _syncControlRepository = scope.ServiceProvider.GetRequiredService<IGenericRepository<SyncControl>>();

                Expression<Func<SyncControl, bool>> entityName = s => s.EntityName == EntityName;
                Expression<Func<SyncControl, Object>> lastSync = s => s.LastSyncDate;

                var sync = await _syncControlRepository.GetFirstOrLastAsync(entityName, lastSync, false);

                return sync?.LastSyncDate ?? DateTime.MinValue;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task CreateSyncControl(SyncControl syncControl)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<HojasDbContext>();
                var _syncControlRepository = scope.ServiceProvider.GetRequiredService<IGenericRepository<SyncControl>>();


                await _syncControlRepository.AddAsync(syncControl);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

    }
}
