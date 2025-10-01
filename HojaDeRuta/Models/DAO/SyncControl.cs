using System.ComponentModel.DataAnnotations;

namespace HojaDeRuta.Models.DAO
{
    public class SyncControl
    {
        [Key]
        public int Id { get; set; }
        public string EntityName { get; set; }
        public DateTime LastSyncDate { get; set; }
        public string Result { get; set; }
    }
}
