using HojaDeRuta.Models.DAO;
using System.Net.Http;
using System;
using HojaDeRuta.DBContext;
using Microsoft.EntityFrameworkCore;
using HojaDeRuta.Models.Config;
using Microsoft.Extensions.Options;
using System.Linq;

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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
       {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var nextRunTime = new DateTime(now.Year, now.Month, now.Day, _syncSettings.RunHour, _syncSettings.RunMinute, 0);

                if (now > nextRunTime)
                {
                    nextRunTime = nextRunTime.AddDays(1);
                }

                var timeUntilNextRun = nextRunTime - now;
                await Task.Delay(timeUntilNextRun, stoppingToken);

                try
                {
                    await SyncContacts(stoppingToken);
                }
                catch (Exception ex)
                {
                    //_logger.LogError(ex, "Error durante la sincronización con Creatio");
                }

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }

        private async Task SyncContacts(CancellationToken token)
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
                DateTime? lastSync = await _sharedService.GetLastSync("Clientes_Creatio");
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

                await _sharedService.CreateSyncControl(syncControl);
            }
            catch (Exception ex)
            {
                syncControl.LastSyncDate = DateTime.UtcNow;
                syncControl.Result = $"Error al sincronizar clientes {ex.Message}";
                await _sharedService.CreateSyncControl(syncControl);
            }
       
        }
    }
}
