using HojaDeRuta.Models.Config;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace HojaDeRuta.Services
{
    public class MailService
    {
        private readonly MailSettings _mailSettings;

        public MailService(IOptions<MailSettings> mailSettings)
        {
            _mailSettings = mailSettings.Value;
        }

        public async Task SendMailAsync(string subject, List<string> destinatarios, string body, bool IsBodyHtml)
        {
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
                    message.To.Add(destinatario);
                }

                await client.SendMailAsync(message);
            }
        }

        public async Task<string> GetBodyInformarRevisor(string url, string numero, string destinatario)
        {
            return $@"
            <html>
              <body style='font-family: Arial, sans-serif; color:#333;'>
                <h3> Estimado {destinatario}:</h3>
                <h3> Ud. fue asignado como revisor para la hoja de Ruta Nº {numero}.<h3>
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

        public async Task<string> GetBodyInformarRechazo(
            string url, string numero, string rechazador, string motivo)
        {
            return $@"
            <html>
              <body style='font-family: Arial, sans-serif; color:#333;'>
                <h3> Estimado revisor:</h3>
                <h3> La hoja de Ruta Nº {numero} fue rechazada por {rechazador}.<h3>
                {
                    (!String.IsNullOrWhiteSpace(motivo)
                        ? $"<p> Motivo: {motivo} </p>"
                        : "")
                }
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

    }
}
