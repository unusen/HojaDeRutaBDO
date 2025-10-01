namespace HojaDeRuta.Models.Config
{
    public class MailSettings
    {
        public string SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public string From { get; set; }
        public string Pass { get; set; }
    }
}
