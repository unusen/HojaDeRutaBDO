namespace HojaDeRuta.Models.Config
{
    public class MailSettings
    {
        public string SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public bool EnableSsl { get; set; }
        public string From { get; set; }
        public string Pass { get; set; }
        public string Dominio { get; set; }
        public string Mail_IT { get; set; }
    }
}
