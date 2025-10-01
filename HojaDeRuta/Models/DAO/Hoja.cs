using HojaDeRuta.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HojaDeRuta.Models.DAO

{
    public class Hoja
    {
        [Key]
        [Column("id")]
        public string Id { get; set; }

        [Column("cliente")]
        public string Cliente { get; set; }

        [Column("ClienteName")]
        public string ClienteName { get; set; }

        [Column("sector")]
        public string Sector { get; set; }

        [Column("subarea")]
        public string Subarea { get; set; }

        [Column("numeracion")]
        public string Numero { get; set; }

        [Column("generico")]
        public string NombreGenerico { get; set; }

        [Column("descripcion")]
        public string Descripcion { get; set; }

        [Column("fecha")]
        public string FechaDocumento { get; set; }

        [Column("revisosocio")]
        public string SocioFirmante { get; set; }

        [NotMapped]
        public bool IsSindico { get; set; }

        [Column("sindica")]
        public string Sindico { get; set; }

        [NotMapped]
        public string CodCliente { get; set; }

        [Column("contrato")]
        public string ContratoPlataforma { get; set; }

        [Column("preparo")]
        public string Preparo { get; set; }

        [Column("preparo_fecha")]
        public string PreparoFecha { get; set; }

        [Column("reviso")]
        public string Reviso { get; set; }

        [Column("reviso_fecha")]
        public string RevisoFecha { get; set; }

        [Column("revisogte")]
        public string RevisionGerente { get; set; }

        [Column("revisogte_fecha")]
        public string RevisionGerenteFecha { get; set; }

        [Column("mailengagement")]
        public string EngagementPartner { get; set; }

        [Column("mailengagement_fecha")]
        public string EngagementPartnerFecha { get; set; }

        [Column("revisosocio_fecha")]
        public string SocioFirmanteFecha { get; set; }

        [Column("fecha_limite")]
        public string FechaLimite { get; set; }

        [Column("manejador_final")]
        public string GestorFinal { get; set; }

        [Column("lugar_firma")]
        public string LugarFirma { get; set; }

        [Column("rutadoc")]
        public string RutaDoc { get; set; }

        [Column("rutapapeles")]
        public string RutaPapeles { get; set; }

        [NotMapped]
        [Required(ErrorMessage = "No se puede crear la hoja sin archivos en la carpeta de documento.")]
        public string Adjuntos { get; set; }

        [Column("observaciones")]
        public string Observaciones { get; set; }

        [Column("estado")]
        public Estado Estado { get; set; }

        //public string id { get; set; }
        //public string usuario { get; set; }
        //public string equipo { get; set; }
        //public string cliente { get; set; }
        //public string sector { get; set; }
        //public string subarea { get; set; }
        //public string numeracion { get; set; }
        //public string fecha { get; set; }
        //public string descripcion { get; set; }
        //public string socio_firmante { get; set; }
        //public string sindica { get; set; }
        //public string contrato { get; set; }
        //public string preparo { get; set; }
        //public string reviso { get; set; }
        //public string revisogte { get; set; }
        //public string revisosocio { get; set; }
        //public string rutapapeles { get; set; }
        //public string rutadoc { get; set; }
        //public string observaciones { get; set; }
        //public string preparo_fecha { get; set; }
        //public string reviso_fecha { get; set; }
        //public string revisogte_fecha { get; set; }
        //public string revisosocio_fecha { get; set; }
        //public string fecha_modif { get; set; }
        //public string hora_modif { get; set; }
        //public int estado { get; set; }
        //public string mailasociado { get; set; }
        //public string generico { get; set; }
        //public string mailengagement { get; set; }
        //public int nivel_doc { get; set; }
        //public string manejador { get; set; }
        //public string fecha_limite { get; set; }
        //public string lugar_firma { get; set; }
        //public string manejador_final { get; set; }
        //public string mailengagement_fecha { get; set; }
    }
}
