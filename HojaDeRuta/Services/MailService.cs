using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DTO;
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

        public async Task SendMailAsync(string subject, string destinatario, string body, bool IsBodyHtml)
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

                message.To.Add(destinatario);

                await client.SendMailAsync(message);
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

    }
}
