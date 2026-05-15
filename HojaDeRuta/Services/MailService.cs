using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;
using HojaDeRuta.Models.Enums;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PuppeteerSharp.Media;
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
                $" con redireccion a la ruta {urlRedireccion}" +
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
                _logger.LogError(ex, "Error al notificar aprobación al revisor {Revisor} para la hoja {Hoja}", eMailBody.Revisor.Empleado, eMailBody.NumeroHoja);
                throw new Exception($"No se pudo enviar la notificación de aprobación por correo electrónico.", ex);
            }
        }

        public async Task NotificarRechazo(EMailBody eMailBody, string rechazador, string urlRedireccion)
        {
            _logger.LogInformation($"Notificación de rechazo de etapa" +
                $" con redireccion a la ruta {urlRedireccion}" +
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
                _logger.LogError(ex, "Error al notificar rechazo al revisor {Revisor} para la hoja {Hoja}", eMailBody.Revisor.Empleado, eMailBody.NumeroHoja);
                throw new Exception($"No se pudo enviar la notificación de rechazo por correo electrónico.", ex);
            }
        }

        public async Task NotificarFirma(EMailBody eMailBody, string firmante, string urlRedireccion)
        {
            _logger.LogInformation($"Notificación de firma de hoja" +
                $" con redireccion a la ruta {urlRedireccion}" +
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
                _logger.LogError(ex, "Error al notificar firma al revisor {Revisor} para la hoja {Hoja}", eMailBody.Revisor.Empleado, eMailBody.NumeroHoja);
                throw new Exception($"No se pudo enviar la notificación de firma por correo electrónico.", ex);
            }
        }

        public async Task NotificarAccesoCruzado(Hoja hoja, string urlRedireccion)
        {
            _logger.LogInformation($"Notificación de accesos cruzados" +
                $" con redireccion a la ruta {urlRedireccion}" +
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
                _logger.LogError(ex, "Error al notificar acceso cruzado para el sector {Sector}", hoja.Sector);
                throw new Exception($"Ocurrió un problema al enviar la notificación de acceso cruzado.", ex);
            }
        }


        public async Task SendMailAsync(string subject, List<string> destinatarios, string body, bool IsBodyHtml)
        {
            _logger.LogInformation($"Envio de email para {subject}." +
               $" Destinatarios {String.Join('-', destinatarios)}." +
               $" Body: {body}");

            try
            {
                string dominio = _mailSettings.Dominio;

                //TODO: TEST PARA ENVIO DE EMAIL
                destinatarios = new List<string>()
                {
                    "sebastian.katcheroff@gmail.com"
                };

                using (var client = new SmtpClient(_mailSettings.SmtpServer, _mailSettings.SmtpPort))
                {
                    client.EnableSsl = _mailSettings.EnableSsl;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(_mailSettings.From, _mailSettings.Pass); //null; // 

                    _logger.LogInformation($"Obtencion de credenciales");

                    var message = new MailMessage
                    {
                        From = new MailAddress(_mailSettings.From),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = IsBodyHtml
                    };

                    _logger.LogInformation($"Armado de MailMessage");

                    foreach (var destinatario in destinatarios)
                    {
                        if (!string.IsNullOrWhiteSpace(destinatario))
                        {                            
                            //message.To.Add($"{destinatario}{dominio}");

                            //TODO: PARA PRUEBAS TEST
                            message.To.Add(destinatario);
                        }
                    }

                    //TODO: HABILITAR EN PROD (POR AHORA NO!)
                    //message.To.Add($"{destinatario}{dominio}");
                    //message.To.Add(destinatario);

                    _logger.LogInformation($"Inicio de envio de Email");

                    await client.SendMailAsync(message);

                    _logger.LogInformation("Mail enviado exitosamente");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falla crítica en el envío de email: {Subject}", subject);
                throw new Exception("El servicio de mensajería no está disponible o las credenciales son incorrectas. Por favor, verifique el log de eventos.", ex);
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
                
                <p> <strong>Ruta de papeles:</strong>
                    <a href='{eMailBody.RutaPapeles}' style='color: #007bff; text-decoration: underline;'>
                    Ir a Ruta de Papeles
                    </a>
                </p>

                <p> <strong>Ruta del doc.:</strong>
                    <a href='{eMailBody.RutaDoc}' style='color: #007bff; text-decoration: underline;'>
                    Ir a Ruta de Documento
                    </a>
                </p>

                <p style='margin-top:20px;'>
                    <!--[if mso]>
                        <v:roundrect xmlns:v=""urn:schemas-microsoft-com:vml""
                                     href='{url}'
                                     style=""height:40px;v-text-anchor:middle;width:200px;""
                                     arcsize=""10%""
                                     strokecolor=""#354997""
                                     fillcolor=""#354997"">
                          <w:anchorlock/>
                          <center style=""color:#ffffff;font-family:Arial,sans-serif;font-size:14px;"">
                            Ver Hoja de Ruta
                          </center>
                        </v:roundrect>
                    <![endif]-->

                    <!--[if !mso]><!-- -->
                        <a href='{url}'
                           style=""display:inline-block;
                                  background-color:#354997;
                                  color:#ffffff;
                                  padding:10px 15px;
                                  text-decoration:none;
                                  border-radius:5px;
                                  font-family:Arial, sans-serif;"">
                           Ver Hoja de Ruta
                        </a>
                    <!--<![endif]-->
                </p>
              </body>
            </html>";

            //CODIGO BOTON ANTERIOR
            /*
             <a href='{url}' 
                     style='background-color:#354997;color:#fff;padding:10px 15px;
                            text-decoration:none;border-radius:5px;'>
                     Ver Hoja de Ruta
                  </a>


            <a href='{url}' 
                       style='color: #354997; font-family: Arial, sans-serif; font-weight: bold; text-decoration: none; font-size: 16px;'>
                        Ver Hoja de Ruta
                    </a>

             */
        }

        public async Task<string> GetBodyInformarGestorFinal(string url, EMailBody eMailBody, string firmante)
        {
            return $@"
            <html>
              <body style='font-family: Arial, sans-serif; color:#333;'>
                <p> Hola {eMailBody.Revisor.Detalle}:<p>
                <p>El socio {firmante} aprobó su Hoja de Ruta</p>
                <p> <strong>Sector:</strong> {eMailBody.Sector} - <strong> Número:</strong> {eMailBody.NumeroHoja} </p>                
                <p> <strong>Ruta de papeles:</strong>
                    <a href='{eMailBody.RutaPapeles}' style='color: #007bff; text-decoration: underline;'>
                    Ir a Ruta de Papeles
                    </a>
                </p>

                <p> <strong>Ruta del doc.:</strong>
                    <a href='{eMailBody.RutaDoc}' style='color: #007bff; text-decoration: underline;'>
                    Ir a Ruta de Documento
                    </a>
                </p>

                <p> <strong>Observaciones:</strong> {eMailBody.Observaciones} </p>

                <p style='margin-top:20px;'>
                    <!--[if mso]>
                        <v:roundrect xmlns:v=""urn:schemas-microsoft-com:vml""
                                     href='{url}'
                                     style=""height:40px;v-text-anchor:middle;width:200px;""
                                     arcsize=""10%""
                                     strokecolor=""#354997""
                                     fillcolor=""#354997"">
                          <w:anchorlock/>
                          <center style=""color:#ffffff;font-family:Arial,sans-serif;font-size:14px;"">
                            Ver Hoja de Ruta
                          </center>
                        </v:roundrect>
                    <![endif]-->

                    <!--[if !mso]><!-- -->
                        <a href='{url}'
                           style=""display:inline-block;
                                  background-color:#354997;
                                  color:#ffffff;
                                  padding:10px 15px;
                                  text-decoration:none;
                                  border-radius:5px;
                                  font-family:Arial, sans-serif;"">
                           Ver Hoja de Ruta
                        </a>
                    <!--<![endif]-->
                </p>
              </body>
            </html>";

            //return $@"
            //<html>
            //  <body style='font-family: Arial, sans-serif; color:#333;'>
            //    <p> Hola {eMailBody.Revisor.Detalle}:<p>
            //    <p>El socio {firmante} aprobó su Hoja de Ruta</p>
            //    <p> <strong>Sector:</strong> {eMailBody.Sector} - <strong> Número:</strong> {eMailBody.NumeroHoja} </p>
            //    <p> <strong>Ruta de papeles:</strong> {eMailBody.RutaPapeles} </p>
            //    <p> <strong>Ruta del doc.:</strong> {eMailBody.RutaDoc} </p>
            //    <p> <strong>Observaciones:</strong> {eMailBody.Observaciones} </p>
                
            //    <p style='margin-top:20px;'>
            //      <a href='{url}' 
            //         style='background-color:#354997;color:#fff;padding:10px 15px;
            //                text-decoration:none;border-radius:5px;'>
            //         Ver Hoja de Ruta
            //      </a>
            //    </p>
            //  </body>
            //</html>";
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

                <p> <strong>Sector:</strong> {eMailBody.Sector} - <strong> Número:</strong> {eMailBody.NumeroHoja} </p>                
                <p> <strong>Ruta de papeles:</strong>
                    <a href='{eMailBody.RutaPapeles}' style='color: #007bff; text-decoration: underline;'>
                    Ir a Ruta de Papeles
                    </a>
                </p>

                <p> <strong>Ruta del doc.:</strong>
                    <a href='{eMailBody.RutaDoc}' style='color: #007bff; text-decoration: underline;'>
                    Ir a Ruta de Documento
                    </a>
                </p>

                <p style='margin-top:20px;'>
                    <!--[if mso]>
                        <v:roundrect xmlns:v=""urn:schemas-microsoft-com:vml""
                                     href='{url}'
                                     style=""height:40px;v-text-anchor:middle;width:200px;""
                                     arcsize=""10%""
                                     strokecolor=""#354997""
                                     fillcolor=""#354997"">
                          <w:anchorlock/>
                          <center style=""color:#ffffff;font-family:Arial,sans-serif;font-size:14px;"">
                            Ver Hoja de Ruta
                          </center>
                        </v:roundrect>
                    <![endif]-->

                    <!--[if !mso]><!-- -->
                        <a href='{url}'
                           style=""display:inline-block;
                                  background-color:#354997;
                                  color:#ffffff;
                                  padding:10px 15px;
                                  text-decoration:none;
                                  border-radius:5px;
                                  font-family:Arial, sans-serif;"">
                           Ver Hoja de Ruta
                        </a>
                    <!--<![endif]-->
                </p>
              </body>
            </html>";
            //return $@"
            //<html>
            //  <body style='font-family: Arial, sans-serif; color:#333;'>
            //    <p> Hola {eMailBody.Revisor.Detalle}:<p>
            //    <p>La hoja de Ruta <strong> Nº {eMailBody.NumeroHoja} </strong> fue rechazada por <strong>{rechazador}.</strong></p>

            //    {(!String.IsNullOrWhiteSpace(eMailBody.MotivoDeRechazo)
            //            ? $"<p> <strong> Motivo de rechazo: </strong> {eMailBody.MotivoDeRechazo} </p>"
            //            : "")}

            //    <p> <strong> Sector: </strong> {eMailBody.Sector} - <strong> Número: </strong> {eMailBody.NumeroHoja} </p>
            //    <p> <strong> Ruta de papeles: </strong> {eMailBody.RutaPapeles} </p>
            //    <p> <strong> Ruta del doc.: </strong> {eMailBody.RutaDoc} </p>

            //    <p style='margin-top:20px;'>
            //      <a href='{url}' 
            //         style='background-color:#354997;color:#fff;padding:10px 15px;
            //                text-decoration:none;border-radius:5px;'>
            //         Ver Hoja de Ruta
            //      </a>
            //    </p>
            //  </body>
            //</html>";
        }

        public async Task<string> GetBodyInformarAccesoCruzado(string url, Hoja hoja, string socioLider)
        {
            return $@"
            <html>
              <body style='font-family: Arial, sans-serif; color:#333;'>
                <p> Hola {socioLider}:<p>
                <p> El socio {hoja.SocioFirmante} solicita acceso a la carpeta
                   {hoja.RutaPapeles} para la revisión de la Hoja de Ruta {hoja.Numero}
                </p>

                <p style='margin-top:20px;'>
                    <!--[if mso]>
                        <v:roundrect xmlns:v=""urn:schemas-microsoft-com:vml""
                                     href='{url}'
                                     style=""height:40px;v-text-anchor:middle;width:200px;""
                                     arcsize=""10%""
                                     strokecolor=""#354997""
                                     fillcolor=""#354997"">
                          <w:anchorlock/>
                          <center style=""color:#ffffff;font-family:Arial,sans-serif;font-size:14px;"">
                            Ver Hoja de Ruta
                          </center>
                        </v:roundrect>
                    <![endif]-->

                    <!--[if !mso]><!-- -->
                        <a href='{url}'
                           style=""display:inline-block;
                                  background-color:#354997;
                                  color:#ffffff;
                                  padding:10px 15px;
                                  text-decoration:none;
                                  border-radius:5px;
                                  font-family:Arial, sans-serif;"">
                           Ver Hoja de Ruta
                        </a>
                    <!--<![endif]-->
                </p>
              </body>
            </html>";
            //return $@"
            //<html>
            //  <body style='font-family: Arial, sans-serif; color:#333;'>
            //    <p> Hola {socioLider}:<p>
            //    <p> El socio {hoja.SocioFirmante} solicita acceso a la carpeta
            //       {hoja.RutaPapeles} para la revisión de la Hoja de Ruta {hoja.Numero}</p>
            //    <p style='margin-top:20px;'>
            //      <a href='{url}' 
            //         style='background-color:#354997;color:#fff;padding:10px 15px;
            //                text-decoration:none;border-radius:5px;'>
            //         Ver Hoja de Ruta
            //      </a>
            //    </p>
            //  </body>
            //</html>";
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
