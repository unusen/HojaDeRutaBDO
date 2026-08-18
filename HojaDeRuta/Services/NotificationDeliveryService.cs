using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;
using Microsoft.Extensions.Options;

namespace HojaDeRuta.Services
{
    public class NotificationDeliveryService : INotificationDeliveryService
    {
        private const int MaxRetryCount = 3;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
        private readonly MailSettings _mailSettings;
        private readonly MailService _mailService;
        private readonly SharedService _sharedService;
        private readonly ILogger<NotificationDeliveryService> _logger;

        public NotificationDeliveryService(
            IOptions<MailSettings> mailSettings,
            MailService mailService,
            SharedService sharedService,
            ILogger<NotificationDeliveryService> logger)
        {
            _mailSettings = mailSettings.Value;
            _mailService = mailService;
            _sharedService = sharedService;
            _logger = logger;
        }

        public async Task SendApprovalAsync(EMailBody eMailBody, string urlRedireccion)
        {
            var subject = $"La hoja de ruta {eMailBody.NumeroHoja} para el cliente {eMailBody.Cliente} requiere su evaluación";
            var body = await _mailService.GetBodyInformarRevisor(urlRedireccion, eMailBody);
            await SendWithRetryAsync(subject, new[] { eMailBody.Revisor?.Mail }, body, true);
        }

        public async Task SendRejectionAsync(EMailBody eMailBody, string rechazador, string urlRedireccion)
        {
            var subject = $"La hoja de ruta {eMailBody.NumeroHoja} para el cliente {eMailBody.Cliente} fue rechazada";
            var body = await _mailService.GetBodyInformarRechazo(urlRedireccion, eMailBody, rechazador);
            await SendWithRetryAsync(subject, new[] { eMailBody.Revisor?.Mail }, body, true);
        }

        public async Task SendSignatureAsync(EMailBody eMailBody, string firmante, string urlRedireccion)
        {
            var subject = $"La hoja de ruta {eMailBody.NumeroHoja} fue aprobada";
            var body = await _mailService.GetBodyInformarGestorFinal(urlRedireccion, eMailBody, firmante);
            await SendWithRetryAsync(subject, new[] { eMailBody.Revisor?.Mail }, body, true);
        }

        public async Task SendCrossAccessAsync(Hoja hoja, string urlRedireccion)
        {
            if (!_mailSettings.HabilitarNotificacionCruzadas)
            {
                _logger.LogInformation(
                    "Se omite la entrega de notificación de acceso cruzado por configuración. HojaId={HojaId}; HabilitarNotificacionCruzadas={HabilitarNotificacionCruzadas}",
                    hoja?.Id ?? "(sin hoja)",
                    _mailSettings.HabilitarNotificacionCruzadas);
                return;
            }

            var subject = "Solicitud de acceso para Hoja de Ruta";
            var socioLider = await _sharedService.GetSocioLiderByArea(new Dictionary<string, string>
            {
                { "Area", hoja.Sector }
            });

            var body = await _mailService.GetBodyInformarAccesoCruzado(urlRedireccion, hoja, socioLider.Detalle);
            await SendWithRetryAsync(subject, new[] { socioLider.Mail, _mailSettings.Mail_IT }, body, true);
        }

        public async Task SendWeeklyPendingAsync(HojaPendiente pendiente)
        {
            var hoja = pendiente.CantidadRegistros == 1 ? "hoja" : "hojas";
            var subject = $"{pendiente.Revisor} tenés {pendiente.CantidadRegistros} {hoja} de ruta sin revisar";
            var body = await _mailService.GetBodyNotificacionSemanal(pendiente);
            await SendWithRetryAsync(subject, new[] { pendiente.Revisor }, body, true);
        }

        private async Task SendWithRetryAsync(string subject, IEnumerable<string?> recipients, string body, bool isBodyHtml)
        {
            var resolvedRecipients = ResolveRecipients(recipients);
            if (!resolvedRecipients.Any())
            {
                throw new InvalidOperationException($"No se encontraron destinatarios válidos para el asunto {subject}.");
            }

            _logger.LogInformation(
                "Preparando entrega de email. Asunto: {Subject}. Destinatarios: {Recipients}. LongitudBody: {BodyLength}",
                subject,
                string.Join(", ", resolvedRecipients),
                body?.Length ?? 0);

            for (var attempt = 1; attempt <= MaxRetryCount + 1; attempt++)
            {
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    using var client = new SmtpClient(_mailSettings.SmtpServer, _mailSettings.SmtpPort)
                    {
                        EnableSsl = _mailSettings.EnableSsl,
                        UseDefaultCredentials = false,
                        Credentials = new NetworkCredential(_mailSettings.From, _mailSettings.Pass)
                    };

                    using var message = new MailMessage
                    {
                        From = new MailAddress(_mailSettings.From),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = isBodyHtml
                    };

                    foreach (var recipient in resolvedRecipients)
                    {
                        message.To.Add(recipient);
                    }

                    _logger.LogInformation(
                        "Intento SMTP {Attempt}/{TotalAttempts}. Servidor: {Server}:{Port}. Asunto: {Subject}",
                        attempt,
                        MaxRetryCount + 1,
                        _mailSettings.SmtpServer,
                        _mailSettings.SmtpPort,
                        subject);

                    await client.SendMailAsync(message);
                    stopwatch.Stop();

                    _logger.LogInformation(
                        "Email enviado correctamente. Asunto: {Subject}. Destinatarios: {Recipients}. Intento: {Attempt}/{TotalAttempts}. DuraciónMs: {DurationMs}",
                        subject,
                        string.Join(", ", resolvedRecipients),
                        attempt,
                        MaxRetryCount + 1,
                        stopwatch.ElapsedMilliseconds);

                    return;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    var isLastAttempt = attempt >= MaxRetryCount + 1;
                    var friendlyMessage = BuildFriendlyErrorMessage(ex);

                    _logger.LogWarning(
                        ex,
                        "Falló el envío de email. Asunto: {Subject}. Intento: {Attempt}/{TotalAttempts}. Destinatarios: {Recipients}. DuraciónMs: {DurationMs}",
                        subject,
                        attempt,
                        MaxRetryCount + 1,
                        string.Join(", ", resolvedRecipients),
                        stopwatch.ElapsedMilliseconds);

                    if (isLastAttempt)
                    {
                        _logger.LogError(
                            ex,
                            "Se agotaron los reintentos del envío de email. Asunto: {Subject}. Destinatarios: {Recipients}. MensajeUsuario: {FriendlyMessage}",
                            subject,
                            string.Join(", ", resolvedRecipients),
                            friendlyMessage);
                        throw new InvalidOperationException(friendlyMessage, ex);
                    }

                    _logger.LogInformation(
                        "Reintentando envío en {DelaySeconds} segundos. Asunto: {Subject}. Próximo intento: {NextAttempt}/{TotalAttempts}",
                        RetryDelay.TotalSeconds,
                        subject,
                        attempt + 1,
                        MaxRetryCount + 1);

                    await Task.Delay(RetryDelay);
                }
            }
        }

        private List<string> ResolveRecipients(IEnumerable<string?> recipients)
        {
            var overrideRecipients = (_mailSettings.OverrideRecipients ?? new List<string>())
                .Where(recipient => !string.IsNullOrWhiteSpace(recipient))
                .Select(recipient => recipient.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (overrideRecipients.Any())
            {
                _logger.LogWarning(
                    "Override de destinatarios activo. Se reemplazarán los destinatarios originales por: {Recipients}",
                    string.Join(", ", overrideRecipients));
                return overrideRecipients;
            }

            return recipients
                .Where(recipient => !string.IsNullOrWhiteSpace(recipient))
                .Select(recipient => NormalizeRecipient(recipient!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private string NormalizeRecipient(string recipient)
        {
            var trimmed = recipient.Trim();
            return trimmed.Contains("@", StringComparison.Ordinal)
                ? trimmed
                : $"{trimmed}{_mailSettings.Dominio}";
        }

        private static string BuildFriendlyErrorMessage(Exception ex)
        {
            if (FindException<SmtpFailedRecipientException>(ex) != null)
            {
                return "No pudimos entregar el email porque la direccion del destinatario no existe o no esta habilitada.";
            }

            if (FindException<SmtpFailedRecipientsException>(ex) != null)
            {
                return "No pudimos entregar el email a uno o mas destinatarios. Revisa las direcciones configuradas.";
            }

            if (FindException<TimeoutException>(ex) != null)
            {
                return "El servidor de correo tardo demasiado en responder. Intenta nuevamente en unos minutos.";
            }

            var smtpException = FindException<SmtpException>(ex);
            var fullMessage = string.Join(" | ", FlattenMessages(ex)).ToLowerInvariant();

            if (ContainsAny(fullMessage, "5.1.1", "user unknown", "mailbox unavailable", "recipient address rejected", "destinatario rechazado"))
            {
                return "No pudimos entregar el email porque la direccion del destinatario no existe o no esta habilitada.";
            }

            if (ContainsAny(fullMessage, "active directory", "directory", "ldap", "dns", "name resolution", "remote name could not be resolved"))
            {
                return "No pudimos enviar el email porque el servicio de directorio no respondio. Intenta nuevamente mas tarde.";
            }

            if (smtpException != null && (
                smtpException.StatusCode == SmtpStatusCode.GeneralFailure ||
                smtpException.StatusCode == SmtpStatusCode.ServiceNotAvailable ||
                smtpException.StatusCode == SmtpStatusCode.TransactionFailed ||
                smtpException.StatusCode == SmtpStatusCode.LocalErrorInProcessing))
            {
                return "No pudimos conectarnos con el servidor de correo. Intenta nuevamente en unos minutos.";
            }

            if (ContainsAny(fullMessage, "smtp", "socket", "connection", "connect", "network"))
            {
                return "No pudimos conectarnos con el servidor de correo. Intenta nuevamente en unos minutos.";
            }

            return "No pudimos enviar el email en este momento. Reintenta mas tarde o revisa la configuracion del destinatario.";
        }

        private static TException? FindException<TException>(Exception? ex) where TException : Exception
        {
            while (ex != null)
            {
                if (ex is TException match)
                {
                    return match;
                }

                ex = ex.InnerException;
            }

            return null;
        }

        private static IEnumerable<string> FlattenMessages(Exception? ex)
        {
            while (ex != null)
            {
                if (!string.IsNullOrWhiteSpace(ex.Message))
                {
                    yield return ex.Message;
                }

                ex = ex.InnerException;
            }
        }

        private static bool ContainsAny(string message, params string[] values)
        {
            return values.Any(value => message.Contains(value, StringComparison.OrdinalIgnoreCase));
        }
    }
}
