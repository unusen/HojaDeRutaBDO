namespace HojaDeRuta.Models.Config
{
    public class SyncSettings
    {
        public int SyncClientesRunHour { get; set; }
        public int SyncClientesRunMinute { get; set; }
        public string NotificacionSemanalDay { get; set; }
        public int NotificacionSemanalHour { get; set; }
        public int NotificacionSemanalMinute { get; set; }
    }
}
