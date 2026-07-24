using System.Text.Json;
using System.Threading.Channels;
using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace HojaDeRuta.Services
{
    public class NotificationQueueService : BackgroundService, INotificationQueueService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(2);
        private static readonly TimeSpan CrossAccessDedupTtl = TimeSpan.FromDays(30);
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
        private readonly Channel<NotificationDispatchRequest> _channel;
        private readonly IDistributedCache _cache;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly MailSettings _mailSettings;
        private readonly ILogger<NotificationQueueService> _logger;
        private readonly SemaphoreSlim _statusLock = new(1, 1);

        public NotificationQueueService(
            IDistributedCache cache,
            IServiceScopeFactory scopeFactory,
            IOptions<MailSettings> mailSettings,
            ILogger<NotificationQueueService> logger)
        {
            _cache = cache;
            _scopeFactory = scopeFactory;
            _mailSettings = mailSettings.Value;
            _logger = logger;
            _channel = Channel.CreateUnbounded<NotificationDispatchRequest>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        }

        public Task QueueApprovalAsync(EMailBody eMailBody, string urlRedireccion, string? title = null)
        {
            var request = new NotificationDispatchRequest
            {
                JobId = Guid.NewGuid().ToString("N"),
                HojaId = eMailBody.HojaId,
                Type = "approval",
                Title = string.IsNullOrWhiteSpace(title) ? "Notificacion al Revisor" : title,
                UrlRedireccion = urlRedireccion,
                EMailBody = CloneEmailBody(eMailBody),
                Recipients = CollectRecipients(eMailBody.Revisor?.Mail)
            };

            return QueueAsync(request);
        }

        public Task QueueRejectionAsync(EMailBody eMailBody, string rechazador, string urlRedireccion)
        {
            var request = new NotificationDispatchRequest
            {
                JobId = Guid.NewGuid().ToString("N"),
                HojaId = eMailBody.HojaId,
                Type = "rejection",
                Title = "Notificación de rechazo",
                UrlRedireccion = urlRedireccion,
                EMailBody = CloneEmailBody(eMailBody),
                Rechazador = rechazador,
                Recipients = CollectRecipients(eMailBody.Revisor?.Mail)
            };

            return QueueAsync(request);
        }

        public Task QueueSignatureAsync(EMailBody eMailBody, string firmante, string urlRedireccion, string? title = null)
        {
            var request = new NotificationDispatchRequest
            {
                JobId = Guid.NewGuid().ToString("N"),
                HojaId = eMailBody.HojaId,
                Type = "signature",
                Title = string.IsNullOrWhiteSpace(title) ? "Notificacion de firma al gestor final" : title,
                UrlRedireccion = urlRedireccion,
                EMailBody = CloneEmailBody(eMailBody),
                Firmante = firmante,
                Recipients = CollectRecipients(eMailBody.Revisor?.Mail)
            };

            return QueueAsync(request);
        }

        public async Task QueueCrossAccessAsync(Hoja hoja, string urlRedireccion)
        {
            if (hoja == null || string.IsNullOrWhiteSpace(hoja.Id))
            {
                return;
            }

            var dedupKey = GetCrossAccessDedupKey(hoja.Id);
            var alreadyQueued = await _cache.GetStringAsync(dedupKey);
            if (!string.IsNullOrWhiteSpace(alreadyQueued))
            {
                _logger.LogInformation(
                    "Se omite la notificaciÃ³n de acceso cruzado porque ya fue programada para la hoja {HojaId}.",
                    hoja.Id);
                return;
            }

            var recipients = await ResolveCrossAccessRecipientsAsync(hoja);
            var request = new NotificationDispatchRequest
            {
                JobId = Guid.NewGuid().ToString("N"),
                HojaId = hoja.Id,
                Type = "cross-access",
                Title = "Solicitud de acceso cruzado",
                UrlRedireccion = urlRedireccion,
                Hoja = CloneHoja(hoja),
                Recipients = recipients
            };

            await _cache.SetStringAsync(
                dedupKey,
                DateTime.UtcNow.ToString("O"),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CrossAccessDedupTtl
                });

            await QueueAsync(request);
        }

        public async Task<IReadOnlyCollection<NotificationStatusSnapshot>> GetStatusesAsync(string hojaId)
        {
            if (string.IsNullOrWhiteSpace(hojaId))
            {
                return Array.Empty<NotificationStatusSnapshot>();
            }

            var statuses = await ReadStatusesAsync(hojaId);
            return statuses
                .OrderByDescending(status => status.UpdatedAtUtc)
                .ToList();
        }

        public async Task<NotificationStatusSnapshot> RetryAsync(string hojaId, string jobId)
        {
            if (string.IsNullOrWhiteSpace(hojaId))
            {
                throw new InvalidOperationException("No se indico la hoja asociada a la notificacion.");
            }

            if (string.IsNullOrWhiteSpace(jobId))
            {
                throw new InvalidOperationException("No se indico la notificacion a reintentar.");
            }

            var statuses = await ReadStatusesAsync(hojaId);
            var failedStatus = statuses.FirstOrDefault(status =>
                status.JobId == jobId &&
                string.Equals(status.Status, NotificationStatuses.Failed, StringComparison.OrdinalIgnoreCase));

            if (failedStatus == null)
            {
                throw new InvalidOperationException("La notificacion indicada ya no se encuentra disponible para reintento.");
            }

            var originalRequest = await ReadRequestAsync(jobId);
            if (originalRequest == null)
            {
                throw new InvalidOperationException("No encontramos los datos necesarios para reintentar la notificacion.");
            }

            var retryRequest = CloneRequest(originalRequest);
            retryRequest.JobId = Guid.NewGuid().ToString("N");
            retryRequest.HojaId = hojaId;
            retryRequest.Title = string.IsNullOrWhiteSpace(originalRequest.Title)
                ? failedStatus.Title
                : originalRequest.Title;
            retryRequest.Recipients = failedStatus.Recipients?.Any() == true
                ? failedStatus.Recipients.ToList()
                : retryRequest.Recipients;

            var now = DateTime.UtcNow;
            await QueueAsync(retryRequest);

            return new NotificationStatusSnapshot
            {
                JobId = retryRequest.JobId,
                HojaId = retryRequest.HojaId,
                Type = retryRequest.Type,
                Title = retryRequest.Title,
                Status = NotificationStatuses.Pending,
                Recipients = retryRequest.Recipients,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NotificationQueueService iniciado.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var request = await _channel.Reader.ReadAsync(stoppingToken);
                    await UpdateStatusAsync(request.HojaId, request.JobId, status =>
                    {
                        status.Status = NotificationStatuses.Processing;
                        status.UpdatedAtUtc = DateTime.UtcNow;
                        status.LastError = null;
                    });

                    using var scope = _scopeFactory.CreateScope();
                    var deliveryService = scope.ServiceProvider.GetRequiredService<INotificationDeliveryService>();

                    _logger.LogInformation(
                        "Procesando notificación en background. JobId: {JobId}. Hoja: {HojaId}. Tipo: {Type}. Destinatarios: {Recipients}",
                        request.JobId,
                        request.HojaId,
                        request.Type,
                        request.Recipients.Any() ? string.Join(", ", request.Recipients) : "No resueltos");

                    try
                    {
                        await ProcessAsync(request, deliveryService);

                        await UpdateStatusAsync(request.HojaId, request.JobId, status =>
                        {
                            status.Status = NotificationStatuses.Completed;
                            status.UpdatedAtUtc = DateTime.UtcNow;
                            status.SentAtUtc = DateTime.UtcNow;
                            status.LastError = null;
                        });

                        _logger.LogInformation(
                            "Notificación completada. JobId: {JobId}. Hoja: {HojaId}. Tipo: {Type}",
                            request.JobId,
                            request.HojaId,
                            request.Type);
                    }
                    catch (Exception ex)
                    {
                        await UpdateStatusAsync(request.HojaId, request.JobId, status =>
                        {
                            status.Status = NotificationStatuses.Failed;
                            status.UpdatedAtUtc = DateTime.UtcNow;
                            status.LastError = ex.Message;
                        });

                        _logger.LogError(
                            ex,
                            "Fallo el envío en background. JobId: {JobId}. Hoja: {HojaId}. Tipo: {Type}",
                            request.JobId,
                            request.HojaId,
                            request.Type);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error no controlado en el procesamiento de la cola de notificaciones.");
                }
            }

            _logger.LogInformation("NotificationQueueService finalizado.");
        }

        private async Task QueueAsync(NotificationDispatchRequest request)
        {
            try
            {
                await SaveRequestAsync(request);

                var now = DateTime.UtcNow;
                await AddStatusAsync(new NotificationStatusSnapshot
                {
                    JobId = request.JobId,
                    HojaId = request.HojaId,
                    Type = request.Type,
                    Title = request.Title,
                    Status = NotificationStatuses.Pending,
                    Recipients = request.Recipients,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });

                await _channel.Writer.WriteAsync(request);

                _logger.LogInformation(
                    "Notificación encolada. JobId: {JobId}. Hoja: {HojaId}. Tipo: {Type}",
                    request.JobId,
                    request.HojaId,
                    request.Type);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "No se pudo encolar la notificación. Hoja: {HojaId}. Tipo: {Type}",
                    request.HojaId,
                    request.Type);

                await SafeRegisterFailedQueueAsync(request, "No pudimos programar la notificación en segundo plano.");
            }
        }

        private async Task ProcessAsync(NotificationDispatchRequest request, INotificationDeliveryService deliveryService)
        {
            switch (request.Type)
            {
                case "approval":
                    await deliveryService.SendApprovalAsync(request.EMailBody!, request.UrlRedireccion);
                    break;
                case "rejection":
                    await deliveryService.SendRejectionAsync(request.EMailBody!, request.Rechazador ?? string.Empty, request.UrlRedireccion);
                    break;
                case "signature":
                    await deliveryService.SendSignatureAsync(request.EMailBody!, request.Firmante ?? string.Empty, request.UrlRedireccion);
                    break;
                case "cross-access":
                    await deliveryService.SendCrossAccessAsync(request.Hoja!, request.UrlRedireccion);
                    break;
                default:
                    throw new InvalidOperationException($"Tipo de notificación no soportado: {request.Type}.");
            }
        }

        private async Task AddStatusAsync(NotificationStatusSnapshot status)
        {
            await _statusLock.WaitAsync();
            try
            {
                var statuses = await ReadStatusesAsync(status.HojaId);
                statuses.RemoveAll(existing => existing.JobId == status.JobId);
                statuses.Add(status);
                await SaveStatusesAsync(status.HojaId, statuses);
            }
            finally
            {
                _statusLock.Release();
            }
        }

        private async Task UpdateStatusAsync(string hojaId, string jobId, Action<NotificationStatusSnapshot> update)
        {
            await _statusLock.WaitAsync();
            try
            {
                var statuses = await ReadStatusesAsync(hojaId);
                var status = statuses.FirstOrDefault(existing => existing.JobId == jobId);
                if (status == null)
                {
                    return;
                }

                update(status);
                await SaveStatusesAsync(hojaId, statuses);
            }
            finally
            {
                _statusLock.Release();
            }
        }

        private async Task<List<NotificationStatusSnapshot>> ReadStatusesAsync(string hojaId)
        {
            var json = await _cache.GetStringAsync(GetCacheKey(hojaId));
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<NotificationStatusSnapshot>();
            }

            return JsonSerializer.Deserialize<List<NotificationStatusSnapshot>>(json, SerializerOptions)
                ?? new List<NotificationStatusSnapshot>();
        }

        private async Task SaveStatusesAsync(string hojaId, List<NotificationStatusSnapshot> statuses)
        {
            var filtered = statuses
                .Where(status => status.CreatedAtUtc >= DateTime.UtcNow.AddDays(-2))
                .OrderByDescending(status => status.UpdatedAtUtc)
                .Take(12)
                .ToList();

            var json = JsonSerializer.Serialize(filtered, SerializerOptions);

            await _cache.SetStringAsync(
                GetCacheKey(hojaId),
                json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheTtl
                });
        }

        private async Task SafeRegisterFailedQueueAsync(NotificationDispatchRequest request, string message)
        {
            try
            {
                var now = DateTime.UtcNow;
                await AddStatusAsync(new NotificationStatusSnapshot
                {
                    JobId = request.JobId,
                    HojaId = request.HojaId,
                    Type = request.Type,
                    Title = request.Title,
                    Status = NotificationStatuses.Failed,
                    Recipients = request.Recipients,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                    LastError = message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tampoco se pudo registrar el estado fallido de la notificación para la hoja {HojaId}.", request.HojaId);
            }
        }

        private static string GetCacheKey(string hojaId)
        {
            return $"notification-status:{hojaId}";
        }

        private static string GetRequestCacheKey(string jobId)
        {
            return $"notification-request:{jobId}";
        }

        private static string GetCrossAccessDedupKey(string hojaId)
        {
            return $"notification-cross-access:{hojaId}";
        }

        private static List<string> CollectRecipients(params string?[] recipients)
        {
            return recipients
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<List<string>> ResolveCrossAccessRecipientsAsync(Hoja hoja)
        {
            using var scope = _scopeFactory.CreateScope();
            var sharedService = scope.ServiceProvider.GetRequiredService<SharedService>();
            var socioLider = await sharedService.GetSocioLiderByArea(new Dictionary<string, string>
            {
                { "Area", hoja.Sector }
            });

            return CollectRecipients(
                NormalizeRecipient(socioLider?.Mail),
                NormalizeRecipient(_mailSettings.Mail_IT));
        }

        private string? NormalizeRecipient(string? recipient)
        {
            if (string.IsNullOrWhiteSpace(recipient))
            {
                return null;
            }

            var trimmed = recipient.Trim();
            return trimmed.Contains("@", StringComparison.Ordinal)
                ? trimmed
                : $"{trimmed}{_mailSettings.Dominio}";
        }

        private async Task SaveRequestAsync(NotificationDispatchRequest request)
        {
            var json = JsonSerializer.Serialize(request, SerializerOptions);
            await _cache.SetStringAsync(
                GetRequestCacheKey(request.JobId),
                json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheTtl
                });
        }

        private async Task<NotificationDispatchRequest?> ReadRequestAsync(string jobId)
        {
            var json = await _cache.GetStringAsync(GetRequestCacheKey(jobId));
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<NotificationDispatchRequest>(json, SerializerOptions);
        }

        private static NotificationDispatchRequest CloneRequest(NotificationDispatchRequest source)
        {
            return new NotificationDispatchRequest
            {
                JobId = source.JobId,
                HojaId = source.HojaId,
                Type = source.Type,
                Title = source.Title,
                UrlRedireccion = source.UrlRedireccion,
                EMailBody = source.EMailBody == null
                    ? null
                    : CloneEmailBody(source.EMailBody),
                Hoja = source.Hoja == null
                    ? null
                    : CloneHoja(source.Hoja),
                Firmante = source.Firmante,
                Rechazador = source.Rechazador,
                Recipients = source.Recipients?.ToList() ?? new List<string>()
            };
        }

        private static EMailBody CloneEmailBody(EMailBody source)
        {
            return new EMailBody
            {
                HojaId = source.HojaId,
                NumeroHoja = source.NumeroHoja,
                Sector = source.Sector,
                RutaDoc = source.RutaDoc,
                RutaPapeles = source.RutaPapeles,
                Cliente = source.Cliente,
                MotivoDeRechazo = source.MotivoDeRechazo,
                Observaciones = source.Observaciones,
                Revisor = source.Revisor == null
                    ? null
                    : new Revisores
                    {
                        Empleado = source.Revisor.Empleado,
                        Mail = source.Revisor.Mail,
                        Detalle = source.Revisor.Detalle,
                        Area = source.Revisor.Area,
                        Cargo = source.Revisor.Cargo
                    }
            };
        }

        private static Hoja CloneHoja(Hoja source)
        {
            return new Hoja
            {
                Id = source.Id,
                Numero = source.Numero,
                Sector = source.Sector,
                RutaPapeles = source.RutaPapeles,
                SocioFirmante = source.SocioFirmante
            };
        }
    }
}
