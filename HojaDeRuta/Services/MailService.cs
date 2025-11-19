using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;
using HojaDeRuta.Models.Enums;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net;
using System.Net.Mail;

namespace HojaDeRuta.Services
{
    public class MailService
    {
        private readonly ILogger<MailService> _logger;
        private readonly MailSettings _mailSettings;
        private readonly SharedService _sharedService;

        public MailService(
            IOptions<MailSettings> mailSettings,
            SharedService sharedService,
            ILogger<MailService> logger)
        {
            _mailSettings = mailSettings.Value;
            _sharedService = sharedService;
            _logger = logger;
        }

        public async Task NotificarAprobacion(EMailBody eMailBody, string urlRedireccion)
        {
            _logger.LogInformation($"Notificación de aprobación de etapa" +
                $" con el objeto {JsonConvert.SerializeObject(eMailBody)}");

            try
            {
                string subject = $"La hoja de ruta {eMailBody.NumeroHoja}" +
                    $" para el cliente {eMailBody.Cliente} requiere su evaluación";

                string body = await GetBodyInformarRevisor(urlRedireccion, eMailBody);

                List<string> destinatarios = new List<string>
                {
                    eMailBody.Revisor.Mail
                };

                await SendMailAsync(subject, destinatarios, body, true);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al notificar al revisor" +
                    $" {eMailBody.Revisor.Empleado}. {ex.Message}");
            }
        }

        public async Task NotificarRechazo(EMailBody eMailBody, string rechazador, string urlRedireccion)
        {
            _logger.LogInformation($"Notificación de rechazo de etapa" +
                $" con el objeto {JsonConvert.SerializeObject(eMailBody)}");

            try
            {
                string subject = $"La hoja de ruta {eMailBody.NumeroHoja}" +
                    $" para el cliente {eMailBody.Cliente} fue rechazada";

                string body = await GetBodyInformarRechazo(urlRedireccion, eMailBody, rechazador);

                List<string> destinatarios = new List<string>
                {
                    eMailBody.Revisor.Mail
                };

                await SendMailAsync(subject, destinatarios, body, true);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al notificar al revisor" +
                    $" {eMailBody.Revisor.Empleado}. {ex.Message}");
            }
        }

        public async Task NotificarFirma(EMailBody eMailBody, string firmante, string urlRedireccion)
        {
            _logger.LogInformation($"Notificación de firma de hoja" +
               $" con el objeto {JsonConvert.SerializeObject(eMailBody)}");
            try
            {
                string subject = $"La hoja de ruta {eMailBody.NumeroHoja}" +
                    $" fue aprobada";

                string body = await GetBodyInformarGestorFinal(urlRedireccion, eMailBody, firmante);

                List<string> destinatarios = new List<string>
                {
                    eMailBody.Revisor.Mail
                };

                await SendMailAsync(subject, destinatarios, body, true);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al notificar al revisor" +
                    $" {eMailBody.Revisor.Empleado}. {ex.Message}");
            }
        }

        public async Task NotificarAccesoCruzado(Hoja hoja, string urlRedireccion)
        {
            _logger.LogInformation($"Notificación de accesos cruzados" +
               $" para la hoja {hoja.Id}");

            try
            {
                string mailIT = _mailSettings.Mail_IT;

                string subject = $"Solicitud de acceso para Hoja de Ruta";

                var parameters = new Dictionary<string, string>
                {
                    { "Area", hoja.Sector }
                };

                Socios socioLider = await _sharedService.GetSocioLiderByArea(parameters);

                string body = await GetBodyInformarAccesoCruzado(urlRedireccion, hoja, socioLider.Detalle);

                List<string> destinatarios = new List<string>
                {
                    socioLider.Mail,mailIT
                };

                await SendMailAsync(subject, destinatarios, body, true);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al notificar el acceso cruzado." +
                    $" Sector: {hoja.Sector}. {ex.Message}");
            }
        }


        public async Task SendMailAsync(string subject, List<string> destinatarios, string body, bool IsBodyHtml)
        {
            _logger.LogInformation($"Envio de email para {subject}." +
               $" Destinatarios {String.Join('-', destinatarios)}" +
               $" Body: {body}");

            try
            {
                //TODO: HABILITAR EN PROD
                //string dominio = _mailSettings.Dominio;

                //TODO: TEST
                destinatarios = new List<string>()
                {
                    "sebastian.katcheroff@gmail.com"
                };

                using (var client = new SmtpClient(_mailSettings.SmtpServer, _mailSettings.SmtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(_mailSettings.From, _mailSettings.Pass);

                    var message = new MailMessage
                    {
                        From = new MailAddress(_mailSettings.From),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = IsBodyHtml
                    };

                    foreach (var destinatario in destinatarios)
                    {
                        if (!string.IsNullOrWhiteSpace(destinatario))
                        {
                            //TODO: HABILITAR EN PROD
                            //message.To.Add($"{destinatario}{dominio}");
                            message.To.Add(destinatario);
                        }
                    }

                    //TODO: HABILITAR EN PROD
                    //message.To.Add($"{destinatario}{dominio}");
                    //message.To.Add(destinatario);

                    await client.SendMailAsync(message);

                    _logger.LogInformation("Mail enviado exitosamente");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al enviar mail {ex.Message}");
            }
        }

        public async Task<string> GetBodyInformarRevisor(string url, EMailBody eMailBody)
        {
            return $@"
            <html>
              <body style='font-family: Arial, sans-serif; color:#333;'>
                <p> Hola {eMailBody.Revisor.Detalle}:<p>
                <p>Se le ha asignado la Hoja de Ruta <strong> Nº {eMailBody.NumeroHoja} </strong> para su revisión.</p>
                <p> <strong>Sector:</strong> {eMailBody.Sector} - <strong> Número:</strong> {eMailBody.NumeroHoja} </p>
                <p> <strong>Ruta de papeles:</strong> {eMailBody.RutaPapeles} </p>
                <p> <strong>Ruta del doc.:</strong> {eMailBody.RutaDoc} </p>
                
                <p style='margin-top:20px;'>
                  <a href='{url}' 
                     style='background-color:#354997;color:#fff;padding:10px 15px;
                            text-decoration:none;border-radius:5px;'>
                     Ver Hoja de Ruta
                  </a>
                </p>
              </body>
            </html>";
        }

        public async Task<string> GetBodyInformarGestorFinal(string url, EMailBody eMailBody, string firmante)
        {
            return $@"
            <html>
              <body style='font-family: Arial, sans-serif; color:#333;'>
                <p> Hola {eMailBody.Revisor.Detalle}:<p>
                <p>El socio {firmante} aprobó su Hoja de Ruta</p>
                <p> <strong>Sector:</strong> {eMailBody.Sector} - <strong> Número:</strong> {eMailBody.NumeroHoja} </p>
                <p> <strong>Ruta de papeles:</strong> {eMailBody.RutaPapeles} </p>
                <p> <strong>Ruta del doc.:</strong> {eMailBody.RutaDoc} </p>
                <p> <strong>Observaciones:</strong> {eMailBody.Observaciones} </p>
                
                <p style='margin-top:20px;'>
                  <a href='{url}' 
                     style='background-color:#354997;color:#fff;padding:10px 15px;
                            text-decoration:none;border-radius:5px;'>
                     Ver Hoja de Ruta
                  </a>
                </p>
              </body>
            </html>";
        }

        public async Task<string> GetBodyInformarRechazo(string url, EMailBody eMailBody, string rechazador)
        {
            return $@"
            <html>
              <body style='font-family: Arial, sans-serif; color:#333;'>
                <p> Hola {eMailBody.Revisor.Detalle}:<p>
                <p>La hoja de Ruta <strong> Nº {eMailBody.NumeroHoja} </strong> fue rechazada por <strong>{rechazador}.</strong></p>

                {(!String.IsNullOrWhiteSpace(eMailBody.MotivoDeRechazo)
                        ? $"<p> <strong> Motivo de rechazo: </strong> {eMailBody.MotivoDeRechazo} </p>"
                        : "")}

                <p> <strong> Sector: </strong> {eMailBody.Sector} - <strong> Número: </strong> {eMailBody.NumeroHoja} </p>
                <p> <strong> Ruta de papeles: </strong> {eMailBody.RutaPapeles} </p>
                <p> <strong> Ruta del doc.: </strong> {eMailBody.RutaDoc} </p>

                <p style='margin-top:20px;'>
                  <a href='{url}' 
                     style='background-color:#354997;color:#fff;padding:10px 15px;
                            text-decoration:none;border-radius:5px;'>
                     Ver Hoja de Ruta
                  </a>
                </p>
              </body>
            </html>";
        }

        public async Task<string> GetBodyInformarAccesoCruzado(string url, Hoja hoja, string socioLider)
        {
            return $@"
            <html>
              <body style='font-family: Arial, sans-serif; color:#333;'>
                <p> Hola {socioLider}:<p>
                <p> El socio {hoja.SocioFirmante} solicita acceso a la carpeta
                   {hoja.RutaPapeles} para la revisión de la Hoja de Ruta {hoja.Numero}</p>
                <p style='margin-top:20px;'>
                  <a href='{url}' 
                     style='background-color:#354997;color:#fff;padding:10px 15px;
                            text-decoration:none;border-radius:5px;'>
                     Ver Hoja de Ruta
                  </a>
                </p>
              </body>
            </html>";
        }

        public async Task<string> GetBodyNotificacionSemanal(HojaPendiente pendiente)
        {
            string hoja = pendiente.CantidadRegistros == 1 ? "la Hoja" : "las Hojas";
            return $@"
            <html>
              <body style='font-family: Arial, sans-serif; color:#333;'>
                <p> Hola {pendiente.Revisor}:<p>
                <p> Tenés pendiente de revisión {hoja} de Ruta {pendiente.HojasAsociadas}</p>
                <p> Por favor ingresa a la aplicación para completar su revisión.</p>
              </body>
            </html>";
        }

    }
}
